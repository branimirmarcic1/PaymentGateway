using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Contracts;

public record PaymentProcessed(
    Guid TransactionId,
    string OrderId,
    string Status,
    string FailureReason,
    string WebhookUrl
);
