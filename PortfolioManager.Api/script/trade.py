import os
import time
import random
import logging
import requests
from bs4 import BeautifulSoup
from pymongo import MongoClient
from datetime import datetime

# --- CONFIGURATION ---
MONGO_URI = "mongodb+srv://ankitjhastudy:oGhjaAZP6pzCfxyc@cluster0.f8css.mongodb.net/?retryWrites=true&w=majority"
DB_NAME = "KineticCapitalDB"
COLLECTION_NAME = "StocksDeepData"

# Session details from your browser
CSRF_TOKEN = "1fYYEMSd8TO29NKLrHTZrL1shQ3laogj"
SESSION_ID = "61se8lwk65pw1ngg64o3v97pixfcxid3"

logging.basicConfig(level=logging.INFO, format='%(message)s')
logger = logging.getLogger(__name__)

def create_session():
    s = requests.Session()
    s.headers.update({
        "User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/123.0.0.0 Safari/537.36",
        "Cookie": f"csrftoken={CSRF_TOKEN}; sessionid={SESSION_ID}",
        "X-Csrftoken": CSRF_TOKEN,
        "Referer": "https://www.screener.in/dash/",
        "Accept-Encoding": "gzip, deflate, br",
        "Connection": "keep-alive"
    })
    return s

def parse_trades(container, mode):
    if not container: return []
    table = container.find('table', class_='data-table')
    if not table: return []
    
    results = []
    current_date = "N/A"
    
    for row in table.find_all('tr'): 
        tds = row.find_all('td')
        if not tds: continue
        
        # Date Header detection
        if len(tds) == 1 or "stripe" in row.get('class', []):
            text = row.get_text(strip=True)
            if any(m in text for m in ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec']):
                current_date = text
                continue
        
        if len(tds) >= 3:
            try:
                if mode == "insider":
                    results.append({
                        "Date": current_date, 
                        "Person": tds[0].get_text(" ", strip=True),
                        "Quantity": tds[1].get_text(strip=True), 
                        "AvgPrice": tds[2].get_text(strip=True),
                        "ValueLacs": tds[3].get_text(strip=True) if len(tds) > 3 else ""
                    })
                elif mode in ["bulk", "block"]:
                    results.append({
                        "Date": current_date, 
                        "Person": tds[0].get_text(strip=True),
                        "Action": tds[1].get_text(strip=True), 
                        "Quantity": tds[2].get_text(strip=True),
                        "Price": tds[3].get_text(strip=True)
                    })
                elif mode == "sast":
                    results.append({
                        "Date": current_date, 
                        "Person": tds[0].get_text(strip=True),
                        "Transaction": tds[1].get_text(strip=True), 
                        "Mode": tds[2].get_text(strip=True),
                        "Quantity": tds[3].get_text(strip=True), 
                        "Percent": tds[4].get_text(strip=True)
                    })
            except: continue
    return results

def run_sync():
    try:
        client = MongoClient(MONGO_URI)
        db = client[DB_NAME]
        col = db[COLLECTION_NAME]
        
        # --- OPTIMIZED QUERY ---
        # 1. TradeId must be valid
        # 2. Skip if we have already synced trades for this stock (LastTradesUpdate exists)
        query = {
            "TradeId": {"$exists": True, "$ne": "NOT_FOUND", "$not": {"$type": "null"}},
            "LastTradesUpdate": {"$exists": False} 
        }
        
        stocks = list(col.find(query))
        if not stocks:
            print("🏁 All stocks are already synced! No new data to pull.")
            return

        print(f"🚀 Found {len(stocks)} pending stocks. Starting High-Speed Sync...")
        session = create_session()

        for index, stock in enumerate(stocks):
            # Session rotation for stability
            if index > 0 and index % 40 == 0:
                session.close()
                time.sleep(random.uniform(5, 10))
                session = create_session()
                print("🔄 Session rotated. Continuing...")

            trade_id = stock.get('TradeId')
            symbol = stock.get('Symbol')
            url = f"https://www.screener.in/trades/company-{trade_id}/"
            
            # Anti-ban jitter
            time.sleep(random.uniform(1.3, 2.7))
            
            try:
                res = session.get(url, timeout=15)
                
                if res.status_code == 200:
                    soup = BeautifulSoup(res.content, 'html.parser')
                    
                    payload = {
                        "Insider": parse_trades(soup.find('div', id='trades-insider-trades'), "insider"),
                        "Bulk": parse_trades(soup.find('div', id='trades-bulk-deals'), "bulk"),
                        "Block": parse_trades(soup.find('div', id='trades-block-deals'), "block"),
                        "Sast": parse_trades(soup.find('div', id='trades-sast-trades'), "sast")
                    }
                    
                    # Update database with data and the timestamp checkpoint
                    col.update_one(
                        {"_id": stock["_id"]}, 
                        {"$set": {
                            "Trades": payload, 
                            "LastTradesUpdate": datetime.utcnow()
                        }}
                    )
                    
                    msg = f"[{index+1}/{len(stocks)}] ✅ {symbol} | I:{len(payload['Insider'])} B:{len(payload['Bulk'])} Blk:{len(payload['Block'])}"
                    print(msg)
                
                elif res.status_code == 429:
                    print(f"🛑 Rate limit at {symbol}! Cooling down for 5 minutes...")
                    time.sleep(300)
                
                elif res.status_code == 403:
                    print("🛑 Session Expired! Please copy fresh SESSION_ID from your browser.")
                    break
                    
            except Exception as e:
                print(f"📡 Request failed for {symbol}: {e}. Moving to next.")
                continue

        client.close()
        print("🏁 Sync complete!")
        
    except Exception as e:
        print(f"💥 Fatal Error: {e}")

if __name__ == "__main__":
    run_sync()