import requests
from bs4 import BeautifulSoup
from pymongo import MongoClient
import time
import os
import re
from pathlib import Path
from dotenv import load_dotenv

# --- 1. SETUP & ENV ---
print("---------------------------------------")
print("🚀 STARTING: Industry & Shareholding Sync")
print("---------------------------------------")

env_path = Path(__file__).resolve().parent.parent / '.env'
load_dotenv(dotenv_path=env_path)

# --- 2. CONFIG (LOGGED-IN SESSION) ---
# Update these if you get a "SESSION_EXPIRED" error
SESSION_VALUE = "61se8lwk65pw1ngg64o3v97pixfcxid3" 
CSRF_VALUE = "7yJwxZoRd8sizsxJetAzaR9VgCK7bRh5"

MONGO_URI = os.getenv("MONGO_URI")
DB_NAME = "KineticCapitalDB"
COLLECTION_NAME = "StocksDeepData"

def scrape_stock_details(symbol, session):
    """Extracts Industry and Shareholding Pattern from Screener"""
    ticker = symbol.split('.')[0].strip()
    url = f"https://www.screener.in/company/{ticker}/"
    
    headers = {
        'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/123.0.0.0 Safari/537.36',
        'Cookie': f'sessionid={SESSION_VALUE}; csrftoken={CSRF_VALUE}', 
        'Referer': 'https://www.screener.in/',
    }

    try:
        res = session.get(url, headers=headers, timeout=15)
        if res.status_code != 200: return None
        
        soup = BeautifulSoup(res.text, 'html.parser')
        
        # Check if session is still alive
        page_title = soup.title.string.strip() if soup.title else ""
        if "Login" in page_title: return "STOP_SESSION_EXPIRED"

        # --- PART A: INDUSTRY EXTRACTION ---
        industry = "N/A"
        industry_tag = soup.find('a', title='Industry')
        if industry_tag:
            industry = industry_tag.get_text(strip=True)

        # --- PART B: SHAREHOLDING EXTRACTION ---
        shareholding = []
        sh_section = soup.find('section', id='shareholding')
        
        if sh_section:
            table = sh_section.find('table', class_='data-table')
            if table:
                # 1. Get Quarters (Headers)
                thead = table.find('thead')
                header_cols = [th.text.strip() for th in thead.find_all('th') if th.text.strip()]
                quarters = header_cols[1:] # Skip 'Description' column
                
                # 2. Get Rows (Promoters, FII, DII, etc.)
                tbody = table.find('tbody')
                for tr in tbody.find_all('tr'):
                    cols = tr.find_all('td')
                    if cols:
                        # Clean category name (removes '+' signs)
                        category = cols[0].get_text(strip=True).replace('+', '').strip()
                        
                        # Create a dictionary of Quarter: Value
                        values = {}
                        for i, col in enumerate(cols[1:]):
                            if i < len(quarters):
                                val = col.get_text(strip=True).replace('%', '')
                                values[quarters[i]] = val
                        
                        shareholding.append({
                            "Category": category,
                            "Values": values
                        })

        return {
            "Industry": industry,
            "Shareholding": shareholding
        }

    except Exception as e:
        print(f"Error scraping {symbol}: {e}")
        return None

def main():
    if not MONGO_URI:
        print("❌ ERROR: MONGO_URI not found!")
        return

    client = MongoClient(MONGO_URI)
    db = client[DB_NAME]
    collection = db[COLLECTION_NAME]

    # RESUME LOGIC: Find stocks where Industry is 'N/A' OR Shareholding field doesn't exist
    query = {
        "$or": [
            {"Industry": "N/A"},
            {"Shareholding": {"$exists": False}},
            {"Shareholding": {"$size": 0}}
        ]
    }
    
    total_to_process = collection.count_documents(query)
    if total_to_process == 0:
        print("✅ Everything is up to date!")
        return

    print(f"🔎 Found {total_to_process} stocks to update. Starting...")

    with requests.Session() as session:
        cursor = collection.find(query)
        
        for i, doc in enumerate(cursor):
            symbol = doc.get("Symbol")
            print(f"[{i+1}/{total_to_process}] Patching {symbol}...", end=" ", flush=True)
            
            data = scrape_stock_details(symbol, session)
            
            if data == "STOP_SESSION_EXPIRED":
                print("\n🛑 SESSION EXPIRED! Log in again and update SESSION_VALUE.")
                break
            
            if data:
                # USE $set to ONLY update these two fields
                collection.update_one(
                    {"_id": doc["_id"]},
                    {"$set": {
                        "Industry": data["Industry"],
                        "Shareholding": data["Shareholding"]
                    }}
                )
                print(f"✅ Industry: {data['Industry']} | SH: {len(data['Shareholding'])} rows")
                # 4-second delay is safe for Screener
                time.sleep(4) 
            else:
                print("❌ Failed")
                time.sleep(2)

if __name__ == "__main__":
    main()