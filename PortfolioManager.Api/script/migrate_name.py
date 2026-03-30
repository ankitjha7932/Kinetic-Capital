import csv
import os
from dotenv import load_dotenv
from pymongo import MongoClient, UpdateOne
from pymongo.errors import BulkWriteError

# Load environment variables from .env file
load_dotenv()

# Configuration
MONGO_URI = os.getenv("MONGO_URI")
DB_NAME = "KineticCapitalDB"
COLLECTION_NAME = "StocksDeepData"
CSV_PATH = "Data/EQUITY_L.csv"

def migrate():
    if not MONGO_URI:
        print("Error: MONGO_URI not found in .env file")
        return

    client = MongoClient(MONGO_URI)
    db = client[DB_NAME]
    collection = db[COLLECTION_NAME]

    updates = []
    processed = 0
    skipped = 0

    print("Starting migration using .env configuration...")

    try:
        with open(CSV_PATH, mode='r', encoding='utf-8') as file:
            reader = csv.DictReader(file)
            for row in reader:
                symbol = row['SYMBOL'].strip()
                name = row['NAME OF COMPANY'].strip()
                
                query = {
                    "Symbol": {"$in": [symbol, f"{symbol}.NS"]},
                    "CompanyName": {"$exists": False} 
                }
                
                updates.append(UpdateOne(query, {"$set": {"CompanyName": name}}))

                if len(updates) >= 100:
                    try:
                        res = collection.bulk_write(updates, ordered=False)
                        processed += res.modified_count
                        skipped += (len(updates) - res.modified_count)
                        print(f"Progress: {processed} updated, {skipped} skipped")
                    except BulkWriteError:
                        pass 
                    
                    updates = []

            if updates:
                res = collection.bulk_write(updates, ordered=False)
                processed += res.modified_count

        print("Migration Finished")
        print(f"Total updated: {processed}")
        print(f"Total skipped: {skipped}")

    except FileNotFoundError:
        print(f"File not found: {CSV_PATH}")
    except Exception as e:
        print(f"Error: {e}")
    finally:
        client.close()

if __name__ == "__main__":
    migrate()