using Contracts;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PaymentProcessor.Models;

public class TransactionLog
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; }

    public Guid TransactionId { get; set; }
    public string OrderId { get; set; }
    public string Status { get; set; }
    public string Reason { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public PaymentRequestReceived RequestData { get; set; }
}
