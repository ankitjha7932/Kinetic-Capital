using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PortfolioManager.Api.Models
{
    public class Holding
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public User User { get; set; } = null!;
        public string Symbol { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal AvgBuyPrice { get; set; }
        public DateTime BuyDate { get; set; }
        public string Tags { get; set; } = ""; 
    }
    
}