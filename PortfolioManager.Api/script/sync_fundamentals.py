import requests
from bs4 import BeautifulSoup
from pymongo import MongoClient
import time
import csv
import os
from pathlib import Path
from dotenv import load_dotenv

# --- CONFIG LOADER ---
# Looks for .env in the root folder (one level up from /script)
env_path = Path(__file__).resolve().parent.parent / '.env'
load_dotenv(dotenv_path=env_path)

MONGO_URI = os.getenv("MONGO_URI")
DB_NAME = "KineticCapitalDB"
COLLECTION_NAME = "StocksDeepData"  # NEW COLLECTION

# Path Logic
SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
CSV_PATH = os.path.abspath(os.path.join(SCRIPT_DIR, "..", "Data", "EQUITY_L.csv"))
STATE_FILE = os.path.join(SCRIPT_DIR, "sync_state_deep.txt")

# Safe timing for heavy data extraction
SLEEP_TIME = 4.0 

def get_last_synced():
    if os.path.exists(STATE_FILE):
        with open(STATE_FILE, "r") as f:
            return f.read().strip()
    return None

def save_last_synced(symbol):
    with open(STATE_FILE, "w") as f:
        f.write(symbol)

def parse_financial_table(section_id, soup):
    """Parses Screener's complex historical tables into JSON-friendly objects"""
    section = soup.find('section', {'id': section_id})
    if not section: return []
    
    table = section.find('table', {'class': 'data-table'})
    if not table: return []

    # 1. Extract Headers (Dates)
    headers = []
    thead = table.find('thead')
    if thead:
        headers = [th.text.strip() for th in thead.find_all('th') if th.text.strip()]
        # Filter: Screener tables often have 'Metric' as first column; we ignore it to get just dates
        if headers and not any(m in headers[0] for m in ["Mar", "Dec", "Sep", "Jun"]):
            headers = headers[1:]

    # 2. Extract Rows (Metrics)
    rows_data = []
    tbody = table.find('tbody')
    if not tbody: return []

    for tr in tbody.find_all('tr'):
        # Skip sub-rows or empty spacers
        if 'class' in tr.attrs and 'sub' in tr['class']: continue
        
        cols = tr.find_all('td')
        if not cols: continue
        
        # Clean the metric name (removes the [+] buttons)
        metric_name = cols[0].text.strip().replace('+', '').replace('—', '').strip()
        
        values = {}
        # Map values to headers
        for i, col in enumerate(cols[1:]):
            if i < len(headers):
                clean_val = col.text.strip().replace(',', '').replace('%', '')
                values[headers[i]] = clean_val
        
        rows_data.append({
            "Metric": metric_name,
            "Values": values
        })
    
    return rows_data

def scrape_screener(symbol):
    ticker = symbol.split('.')[0].strip()
    url = f"https://www.screener.in/company/{ticker}/consolidated/"
    headers = {'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36'}

    try:
        res = requests.get(url, headers=headers, timeout=20)
        if res.status_code != 200: return None
        
        soup = BeautifulSoup(res.text, 'html.parser')
        
        # --- Industry Header ---
        industry = "N/A"
        sub_text = soup.find('p', {'class': 'sub'})
        if sub_text:
            parts = sub_text.text.split(':')
            industry = parts[1].strip() if len(parts) > 1 else "N/A"

        # --- Top Ratios ---
        ratios = {}
        ratio_div = soup.find('div', {'class': 'company-ratios'})
        if ratio_div:
            for li in ratio_div.find_all('li'):
                n, v = li.find('span', {'class': 'name'}), li.find('span', {'class': 'number'})
                if n and v:
                    ratios[n.text.strip().replace(" ", "")] = v.text.strip().replace(',', '')

        # --- Peer Comparison ---
        peers = []
        peer_section = soup.find('section', {'id': 'peers'})
        if peer_section:
            table = peer_section.find('table', {'class': 'data-table'})
            if table:
                for row in table.find('tbody').find_all('tr'):
                    cols = row.find_all('td')
                    if len(cols) > 4:
                        peers.append({
                            "Name": cols[1].text.strip(),
                            "Price": cols[2].text.strip(),
                            "PE": cols[3].text.strip(),
                            "MarketCap": cols[4].text.strip(),
                            "ROCE": cols[-1].text.strip()
                        })

        # Return Mega Document
        return {
            "Symbol": f"{ticker}.NS",
            "Industry": industry,
            "MarketCap": ratios.get("MarketCap", "0"),
            "StockPE": ratios.get("StockP/E", "N/A"),
            "ROCE": ratios.get("ROCE", "N/A"),
            "ROE": ratios.get("ROE", "N/A"),
            "DividendYield": ratios.get("DividendYield", "N/A"),
            "BookValue": ratios.get("BookValue", "N/A"),
            "LastUpdated": time.time(),
            "Peers": peers,
            "QuarterlyResults": parse_financial_table('quarters', soup),
            "ProfitAndLoss": parse_financial_table('profit-loss', soup),
            "BalanceSheet": parse_financial_table('balance-sheet', soup),
            "CashFlow": parse_financial_table('cash-flow', soup)
        }
    except Exception as e:
        print(f"\n Error with {symbol}: {e}")
        return None

def main():
    if not MONGO_URI:
        print(" Error: MONGO_URI missing in .env!")
        return

    client = MongoClient(MONGO_URI)
    db = client[DB_NAME]
    collection = db[COLLECTION_NAME]

    # Load CSV Symbols
    all_symbols = []
    if not os.path.exists(CSV_PATH):
        print(f" Error: CSV not found at {CSV_PATH}")
        return

    with open(CSV_PATH, 'r', encoding='utf-8-sig') as f:
        reader = csv.DictReader(f)
        for row in reader:
            clean_row = {k.strip().upper(): v for k, v in row.items() if k}
            if clean_row.get('SERIES') == 'EQ':
                symbol = clean_row.get('SYMBOL')
                if symbol: all_symbols.append(symbol.strip())

    last_synced = get_last_synced()
    start_index = all_symbols.index(last_synced) + 1 if last_synced in all_symbols else 0

    print(f"BUILDING NEW COLLECTION: {COLLECTION_NAME}")
    print(f"Total Symbols: {len(all_symbols)} | Resuming from: {start_index}")

    for i in range(start_index, len(all_symbols)):
        sym = all_symbols[i]
        print(f"[{i+1}/{len(all_symbols)}] Harvesting Deep Data: {sym}...", end="\r")
        
        data = scrape_screener(sym)
        if data:
            collection.update_one({"Symbol": data["Symbol"]}, {"$set": data}, upsert=True)
            save_last_synced(sym)
            time.sleep(SLEEP_TIME)
        else:
            print(f"\n Skipped {sym} (Network issue or No Data)")

if __name__ == "__main__":
    main()