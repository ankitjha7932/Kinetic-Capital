using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace PortfolioManager.Api.Models
{
    public class Holding
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

        [BsonRepresentation(BsonType.ObjectId)]
        public string UserId { get; set; } 
        public string Symbol { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal AvgBuyPrice { get; set; }
        public DateTime BuyDate { get; set; }
        public string Tags { get; set; } = "";
    }
    
}