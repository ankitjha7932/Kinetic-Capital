import os
import time
import random
import logging
import pymongo
from bs4 import BeautifulSoup
import undetected_chromedriver as uc
from dotenv import load_dotenv
from colorama import Fore, Style, init

# Initialize Colors for Terminal
init(autoreset=True)

def setup_logger():
    logger = logging.getLogger("FinalSync")
    logger.setLevel(logging.INFO)
    fmt = logging.Formatter("%(asctime)s | %(message)s", datefmt='%H:%M:%S')
    ch = logging.StreamHandler()
    ch.setFormatter(fmt)
    logger.addHandler(ch)
    return logger

log = setup_logger()

def run_final_sync():
    log.info(Fore.CYAN + "--- STARTING FINAL HARVEST & SYMBOL MAPPING ---")
    load_dotenv()
    
    client = pymongo.MongoClient(os.getenv("MONGO_URI"))
    db = client["KineticCapitalDB"]
    stocks_col = db["StocksDeepData"]

    # 1. QUEUE: Find stocks missing PeersData (Resumes at TOTAL.NS)
    query = {"ScreenerId": {"$exists": True, "$ne": "NOT_FOUND"}, "PeersData": {"$exists": False}}
    stocks_to_sync = list(stocks_col.find(query, {"Symbol": 1}))
    total_remaining = len(stocks_to_sync)
    
    if total_remaining == 0:
        log.info(Fore.GREEN + "No stocks left to harvest. Moving to Symbol Mapping...")
    else:
        log.info(f"Resuming harvest for {total_remaining} stocks.")
        
        options = uc.ChromeOptions()
        options.add_argument('--blink-settings=imagesEnabled=false') # Fast loading
        
        try:
            driver = uc.Chrome(version_main=146, options=options)
            driver.get("https://www.screener.in/company/NCC/")
            input(Fore.YELLOW + "Solve Captcha/Login, then press ENTER to finish the last batch...")

            for i, doc in enumerate(stocks_to_sync, 1):
                symbol = doc['Symbol']
                ticker = symbol.split('.')[0]
                url = f"https://www.screener.in/company/{ticker}/"
                
                try:
                    driver.get(url)
                    time.sleep(random.uniform(5, 7)) # Human pacing

                    soup = BeautifulSoup(driver.page_source, 'html.parser')
                    table = soup.find('table', class_='data-table')
                    
                    if table:
                        rows = table.find('tbody').find_all('tr')
                        peers_list = []
                        for row in rows:
                            cols = row.find_all('td')
                            if len(cols) < 11: continue
                            
                            peers_list.append({
                                "Name": cols[1].text.strip(),
                                "PE": cols[3].text.strip(),
                                "MarketCap": cols[4].text.strip(),
                                "DivYield": cols[5].text.strip(),
                                "NetProfitQtr": cols[6].text.strip(),
                                "ProfitVarQtr": cols[7].text.strip(),
                                "SalesQtr": cols[8].text.strip(),
                                "SalesVarQtr": cols[9].text.strip(),
                                "ROCE": cols[10].text.strip(),
                                "Symbol": None # To be filled by mapper below
                            })

                        stocks_col.update_one({"Symbol": symbol}, {"$set": {"PeersData": peers_list}})
                        log.info(f"[{i:03}/{total_remaining:03}] SUCCESS: {symbol}")
                    else:
                        log.warning(f"[{i:03}/{total_remaining:03}] BLOCKED/MISSING: {symbol}")
                        time.sleep(20) # Cooling off

                except Exception as e:
                    if "invalid session id" in str(e):
                        log.error("💥 Browser session crashed. Please restart the script.")
                        break
                    log.error(f"ERR: {symbol} - {str(e)[:40]}")
                
                time.sleep(random.uniform(2, 4))

        finally:
            try: driver.quit()
            except: pass

    # 2. SYMBOL MAPPING: Turn "Inventurus Knowl" into "IKS.NS"
    log.info(Fore.CYAN + "--- STARTING INTERNAL SYMBOL MAPPING ---")
    all_stocks_with_peers = stocks_col.find({"PeersData": {"$exists": True, "$ne": []}})
    
    map_count = 0
    for doc in all_stocks_with_peers:
        parent_symbol = doc['Symbol']
        peers = doc['PeersData']
        updated = False

        for peer in peers:
            if not peer.get('Symbol'): # Only map if missing
                name_to_find = peer['Name'].strip()
                
                # The "Contains" logic
                match = stocks_col.find_one(
                    {"CompanyName": {"$regex": name_to_find, "$options": "i"}},
                    {"Symbol": 1}
                )
                if match:
                    peer['Symbol'] = match['Symbol']
                    updated = True

        if updated:
            stocks_col.update_one({"Symbol": parent_symbol}, {"$set": {"PeersData": peers}})
            map_count += 1

    log.info(Fore.GREEN + f"✅ ALL DONE! {map_count} stocks now have fully linked peers.")

if __name__ == "__main__":
    run_final_sync()