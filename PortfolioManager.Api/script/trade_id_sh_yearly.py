import os
import re
import logging
import requests
import time
import random
from bs4 import BeautifulSoup
from pymongo import MongoClient
from datetime import datetime
from dotenv import load_dotenv

# --- INITIALIZATION ---
load_dotenv()
MONGO_URI = os.getenv("MONGO_URI")
DB_NAME = "KineticCapitalDB"
COLLECTION_NAME = "StocksDeepData"

HEADERS = {
    "User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36",
    "Accept": "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8",
    "Accept-Language": "en-US,en;q=0.5",
}

logging.basicConfig(level=logging.INFO, format='%(asctime)s | %(levelname)s | %(message)s')
logger = logging.getLogger(__name__)

def get_ids_robust(soup, html_text):
    """
    Tries multiple methods to find TradeId (Company ID) and WarehouseId.
    """
    results = {"tradeId": None, "warehouseId": None}
    
    # Method 1: Regex on the Global JS Config (Most reliable if present)
    warehouse_match = re.search(r'warehouse_id":\s*(\d+)', html_text)
    company_match = re.search(r'company_id":\s*(\d+)', html_text)
    
    if warehouse_match: results["warehouseId"] = warehouse_match.group(1)
    if company_match: results["tradeId"] = company_match.group(1)

    # Method 2: Look for 'data-company-id' or 'data-warehouse-id' in body/divs
    if not results["tradeId"]:
        meta_div = soup.find("div", {"data-company-id": True})
        if meta_div: results["tradeId"] = meta_div["data-company-id"]
        
    # Method 3: Parse from the "Export to Excel" or "Follow" buttons
    if not results["tradeId"]:
        follow_btn = soup.find("button", {"data-company-id": True})
        if follow_btn: results["tradeId"] = follow_btn["data-company-id"]

    return results

def parse_yearly_shareholding(soup):
    yearly_data = []
    section = soup.find('section', id='shareholding')
    if not section: return []

    # Screener often has two tables: [0] is Quarterly, [1] is Yearly
    tables = section.find_all('table', class_='data-table')
    target_table = None

    for t in tables:
        header_text = t.find('thead').get_text()
        # Yearly headers only contain Mar/Sep and Year, no Jun/Dec usually for older years
        if "Mar 20" in header_text or "Mar 1" in header_text:
            target_table = t
            break
    
    if not target_table: return []

    headers = [th.get_text(strip=True) for th in target_table.find('thead').find_all('th') if th.get_text(strip=True)]
    rows = target_table.find('tbody').find_all('tr')
    
    for row in rows:
        cols = row.find_all('td')
        if len(cols) < 2: continue
        category = cols[0].get_text(strip=True).replace('+', '').strip()
        values = {headers[i]: col.get_text(strip=True) for i, col in enumerate(cols[1:]) if i < len(headers)}
        yearly_data.append({"Category": category, "Values": values})
        
    return yearly_data

def sync_ids_and_yearly():
    client = MongoClient(MONGO_URI)
    db = client[DB_NAME]
    col = db[COLLECTION_NAME]

    # Process stocks missing TradeId
    stocks = list(col.find({"TradeId": {"$in": [None, ""]}}))
    logger.info(f"Starting sync for {len(stocks)} stocks...")

    for stock in stocks:
        symbol = stock.get('Symbol')
        slug = symbol.split('.')[0]
        url = f"https://www.screener.in/company/{slug}/consolidated/"
        
        retry_count = 0
        while retry_count < 3:
            try:
                # Random delay to look human (2-5 seconds)
                time.sleep(random.uniform(2, 5))
                
                response = requests.get(url, headers=HEADERS, timeout=15)
                
                if response.status_code == 429:
                    wait_time = (retry_count + 1) * 30
                    logger.warning(f"Rate limited (429) on {symbol}. Waiting {wait_time}s...")
                    time.sleep(wait_time)
                    retry_count += 1
                    continue

                if response.status_code != 200:
                    logger.error(f"Failed {symbol}: {response.status_code}")
                    break

                soup = BeautifulSoup(response.content, 'html.parser')
                ids = get_ids_robust(soup, response.text)
                yearly_sh = parse_yearly_shareholding(soup)
                
                if not ids["tradeId"]:
                    logger.warning(f"⚠️ Could not find IDs for {symbol}")

                col.update_one(
                    {"_id": stock["_id"]},
                    {"$set": {
                        "TradeId": ids["tradeId"],
                        "ScreenerId": ids["warehouseId"] or stock.get("ScreenerId"),
                        "ShareholdingYearly": yearly_sh,
                        "LastIdSync": datetime.utcnow()
                    }}
                )
                logger.info(f"✅ {symbol}: TradeId={ids['tradeId']}")
                break # Success, exit retry loop

            except Exception as e:
                logger.error(f"Error {symbol}: {e}")
                break

    client.close()

if __name__ == "__main__":
    sync_ids_and_yearly()