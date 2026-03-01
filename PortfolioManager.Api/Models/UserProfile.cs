using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PortfolioManager.Api.Models
{
    public class UserProfile
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public User User { get; set; } = null!;
        public string Broker { get; set; } = ""; // Future: Zerodha, Upstox
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}