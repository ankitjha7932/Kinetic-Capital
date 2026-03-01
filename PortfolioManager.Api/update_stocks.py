import pandas as pd
import requests
import json

def update_stock_list():
    url = "https://archives.nseindia.com/content/equities/EQUITY_L.csv"
    headers = {'User-Agent': 'Mozilla/5.0'} 
    
    try:
        print("Downloading latest stock list from NSE...")
        response = requests.get(url, headers=headers, timeout=15)
        response.raise_for_status()
        
        with open("EQUITY_L.csv", 'wb') as f:
            f.write(response.content)
        
        df = pd.read_csv("EQUITY_L.csv")
        df.columns = df.columns.str.strip() 
        df = df[df['SERIES'].str.strip() == 'EQ']
        
        master_list = []
        for _, row in df.iterrows():
            master_list.append({
                "Symbol": f"{row['SYMBOL']}.NS",
                "Name": row['NAME OF COMPANY'],
                "Sector": "Equity" 
            })
        
        with open("nifty_master.json", "w") as f:
            json.dump(master_list, f, indent=4)
            
        print(f"✅ Success! Saved {len(master_list)} stocks to nifty_master.json")

    except Exception as e:
        print(f" Error: {e}")

if __name__ == "__main__":
    update_stock_list()