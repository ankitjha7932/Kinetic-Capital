import os
import logging
import re
from pymongo import MongoClient, UpdateOne
from dotenv import load_dotenv

# --- INITIALIZATION ---
load_dotenv()
MONGO_URI = os.getenv("MONGO_URI")
DB_NAME = "KineticCapitalDB"
COLLECTION_NAME = "StocksDeepData"

logging.basicConfig(level=logging.INFO, format='%(asctime)s | %(message)s', datefmt='%H:%M:%S')
logger = logging.getLogger(__name__)

def get_word_tokens(name):
    """Clean name and split into unique words/tokens."""
    if not name: return []
    # Remove business suffixes that confuse matching
    name = name.lower()
    name = re.sub(r'\b(limited|ltd|industries|inds|industry|services|corp|corporation|mgmt|fin|pharma)\b', '', name)
    # Extract only alphanumeric words
    return [w for w in re.findall(r'\w+', name) if len(w) > 1]

def is_prefix_match(peer_tokens, company_tokens):
    """
    Checks if tokens in peer name are prefixes of words in company name.
    Example: ['samvardh', 'mothe'] matches ['samvardhana', 'motherson']
    """
    if not peer_tokens or not company_tokens:
        return False
    
    matches = 0
    for p_tok in peer_tokens:
        for c_tok in company_tokens:
            if c_tok.startswith(p_tok):
                matches += 1
                break
    
    # We require at least 2 word matches for longer names, or all matches for short ones
    return matches >= min(len(peer_tokens), 2)

def smart_peer_repair():
    client = MongoClient(MONGO_URI)
    db = client[DB_NAME]
    col = db[COLLECTION_NAME]

    try:
        # 1. PRE-FETCH ALL DATA
        logger.info("Indexing primary stocks for smart matching...")
        cursor = col.find({}, {"Symbol": 1, "CompanyName": 1})
        
        # We store tokens for every company
        # Example: 'MOTHERSON.NS' -> ['samvardhana', 'motherson', 'international']
        company_data = []
        for doc in cursor:
            name = doc.get("CompanyName")
            sym = doc.get("Symbol")
            if name and sym:
                company_data.append({
                    "symbol": sym,
                    "tokens": get_word_tokens(name)
                })

        # 2. SCAN FOR BROKEN PEER LINKS
        logger.info("Scanning Peer Intelligence tables...")
        docs_with_peers = list(col.find({"PeersData": {"$exists": True, "$ne": []}}, {"Symbol": 1, "PeersData": 1}))
        
        bulk_updates = []
        fixed_links = 0

        for doc in docs_with_peers:
            peers = doc.get("PeersData", [])
            needs_update = False
            
            for peer in peers:
                if not peer.get("Symbol"):
                    peer_name = peer.get("Name", "")
                    peer_tokens = get_word_tokens(peer_name)
                    
                    # Try to find a smart match
                    found_symbol = None
                    for comp in company_data:
                        if is_prefix_match(peer_tokens, comp["tokens"]):
                            found_symbol = comp["symbol"]
                            break
                    
                    if found_symbol:
                        peer["Symbol"] = found_symbol
                        needs_update = True
                        fixed_links += 1
            
            if needs_update:
                bulk_updates.append(UpdateOne(
                    {"_id": doc["_id"]},
                    {"$set": {"PeersData": peers}}
                ))

        # 3. EXECUTE
        if bulk_updates:
            logger.info(f"Applying {fixed_links} smart fixes across {len(bulk_updates)} companies...")
            col.bulk_write(bulk_updates, ordered=False)
            logger.info("✅ SUCCESS: The 'Samvardh. Mothe.' type issues are fixed!")
        else:
            logger.info("✅ No new links found to repair.")

    finally:
        client.close()

if __name__ == "__main__":
    smart_peer_repair()