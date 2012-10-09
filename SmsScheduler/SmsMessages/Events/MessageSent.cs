﻿using System;
using NServiceBus;
using SmsMessages.CommonData;

namespace SmsMessages.Events
{
    public class MessageSent : IMessage
    {
        public string Receipt { get; set; }

        public Guid CorrelationId { get; set; }

        public SmsData SmsData { get; set; }

        public SmsMetaData SmsMetaData { get; set; }
    }
}