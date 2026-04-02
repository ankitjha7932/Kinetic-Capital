import os
import time
import random
import logging
import pymongo
import requests
from colorama import Fore, Style, init
from dotenv import load_dotenv

# Initialize Colors for Windows Terminal
init(autoreset=True)

def setup_logger():
    logger = logging.getLogger("IdMapper")
    logger.setLevel(logging.DEBUG)
    # Simple format to avoid encoding issues with emojis in some terminals
    fmt = logging.Formatter("%(asctime)s | %(message)s", datefmt='%H:%M:%S')
    ch = logging.StreamHandler()
    ch.setFormatter(fmt)
    logger.addHandler(ch)
    return logger

log = setup_logger()

def run_id_mapper():
    log.info(f"{Fore.CYAN}--- STARTING SCREENER ID MAPPING ---")
    load_dotenv()
    
    try:
        client = pymongo.MongoClient(os.getenv("MONGO_URI"))
        db = client["KineticCapitalDB"]
        stocks_col = db["StocksDeepData"]
        log.info("Connected to MongoDB successfully.")
    except Exception as e:
        log.error(f"MongoDB Connection Failed: {e}")
        return

    # 1. DEFINE THE QUEUE (Skip if ScreenerId already exists and is not empty)
    query = {
        "$or": [
            {"ScreenerId": {"$exists": False}},
            {"ScreenerId": ""},
            {"ScreenerId": None}
        ]
    }
    
    stocks_to_map = list(stocks_col.find(query, {"Symbol": 1}))
    total_to_do = len(stocks_to_map)
    
    if total_to_do == 0:
        log.info(f"{Fore.GREEN}All stocks already have Screener IDs! Nothing to do.")
        return

    log.info(f"Queue: {total_to_do} stocks need mapping.")

    # Setup Session
    session = requests.Session()
    session.headers.update({
        'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36',
        'X-Requested-With': 'XMLHttpRequest'
    })

    success_count = 0
    not_found_count = 0

    try:
        for i, doc in enumerate(stocks_to_map, 1):
            symbol = doc['Symbol']
            # Clean ticker: "RELIANCE.NS" -> "RELIANCE"
            search_query = symbol.split('.')[0].upper()
            
            search_url = f"https://www.screener.in/api/company/search/?q={search_query}"
            
            try:
                # Speed: API calls are much faster than page loads
                response = session.get(search_url, timeout=10)
                
                if response.status_code == 200:
                    results = response.json()
                    sid = None
                    
                    if results:
                        # Logic: Find exact match first, else take the top result
                        for item in results:
                            url_slug = item.get('url', '').strip('/').split('/')[-1].upper()
                            if url_slug == search_query:
                                sid = str(item.get('id'))
                                break
                        
                        if not sid:
                            sid = str(results[0].get('id'))

                    if sid:
                        stocks_col.update_one(
                            {"Symbol": symbol},
                            {"$set": {"ScreenerId": sid}}
                        )
                        print(f"[{i:04}/{total_to_do:04}] {Fore.GREEN}OK: {symbol} -> {sid}")
                        success_count += 1
                    else:
                        stocks_col.update_one(
                            {"Symbol": symbol},
                            {"$set": {"ScreenerId": "NOT_FOUND"}}
                        )
                        print(f"[{i:04}/{total_to_do:04}] {Fore.YELLOW}??: {symbol} -> Not Found")
                        not_found_count += 1

                elif response.status_code == 429:
                    log.error(f"RATE LIMITED! Status 429. Sleeping for 2 minutes...")
                    time.sleep(120)
                else:
                    log.warning(f"Unexpected status {response.status_code} for {symbol}")

            except Exception as e:
                log.error(f"Error processing {symbol}: {str(e)[:50]}")

            # Smart Pacing: 1.5 to 3 seconds is safe for API endpoints
            time.sleep(random.uniform(1.5, 3.0))

    except KeyboardInterrupt:
        log.warning("Process interrupted by user. Progress saved in MongoDB.")
    
    finally:
        log.info(f"\n{Fore.CYAN}=== FINAL SUMMARY ===")
        log.info(f"Successfully Mapped: {success_count}")
        log.info(f"Marked 'NOT_FOUND': {not_found_count}")
        log.info(f"Remaining in DB: {total_to_do - success_count - not_found_count}")
        log.info(f"{Fore.CYAN}======================")

if __name__ == "__main__":
    run_id_mapper()