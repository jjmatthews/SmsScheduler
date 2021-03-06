using System;
using System.Linq;
using NServiceBus;
using SmsMessages.Coordinator.Events;
using SmsTrackingMessages.Messages;
using SmsTrackingModels;

namespace SmsTracking
{
    public class CoordinatorTracker :
        IHandleMessages<CoordinatorCreated>,
        IHandleMessages<CoordinatorCompleted>
    {
        public IRavenDocStore RavenStore { get; set; }

        public IBus Bus { get; set; }

        public void Handle(CoordinatorCreated message)
        {
            using (var session = RavenStore.GetStore().OpenSession())
            {
                session.Advanced.UseOptimisticConcurrency = true;
                var coordinatorTrackingData = new CoordinatorTrackingData
                {
                    CoordinatorId = message.CoordinatorId,
                    MessageStatuses = message.ScheduledMessages
                        .Select(s => new MessageSendingStatus { Number = s.Number, ScheduledSendingTimeUtc = s.ScheduledTimeUtc, ScheduleMessageId = s.ScheduleMessageId }).
                        ToList(),
                    CreationDateUtc = message.CreationDateUtc,
                    MetaData = message.MetaData,
                    ConfirmationEmailAddress = message.ConfirmationEmailAddress
                };
                session.Store(coordinatorTrackingData, message.CoordinatorId.ToString());
                session.SaveChanges();
            }
        }

        public void Handle(CoordinatorCompleted coordinatorCompleted)
        {
            using (var session = RavenStore.GetStore().OpenSession())
            {
                session.Advanced.UseOptimisticConcurrency = true;
                var coordinatorTrackingData = session.Load<CoordinatorTrackingData>(coordinatorCompleted.CoordinatorId.ToString());
                var incompleteMessageCount = coordinatorTrackingData.MessageStatuses.Count(m => m.Status == MessageStatusTracking.Paused || m.Status == MessageStatusTracking.Scheduled);
                if (incompleteMessageCount > 0)
                    throw new Exception("Cannot complete coordinator - some messages are not yet complete.");
                coordinatorTrackingData.CurrentStatus = CoordinatorStatusTracking.Completed;
                coordinatorTrackingData.CompletionDateUtc = coordinatorCompleted.CompletionDateUtc;
                if (!string.IsNullOrWhiteSpace(coordinatorTrackingData.ConfirmationEmailAddress))
                {
                    var coordinatorCompleteEmail = new CoordinatorCompleteEmail();
                    coordinatorCompleteEmail.CoordinatorId = coordinatorTrackingData.CoordinatorId;
                    coordinatorCompleteEmail.EmailAddress = coordinatorTrackingData.ConfirmationEmailAddress;
                    coordinatorCompleteEmail.FinishTimeUtc = coordinatorTrackingData.CompletionDateUtc.Value;
                    coordinatorCompleteEmail.StartTimeUtc = coordinatorTrackingData.CreationDateUtc;
                    coordinatorCompleteEmail.SendingData = new SendingData
                    {
                        SuccessfulMessages = coordinatorTrackingData.MessageStatuses
                            .Where(m => m.Status == MessageStatusTracking.CompletedSuccess)
                            .Select(m => new SuccessfulMessage
                            {
                                Cost = m.Cost.Value, 
                                ScheduleId = m.ScheduleMessageId, 
                                TimeSentUtc = m.ActualSentTimeUtc.Value
                            })
                            .ToList(),
                        UnsuccessfulMessageses = coordinatorTrackingData.MessageStatuses
                            .Where(m => m.Status == MessageStatusTracking.CompletedFailure)
                            .Select(m => new UnsuccessfulMessage
                            {
                                ScheduleId = m.ScheduleMessageId, 
                                FailureReason = new FailureReason { Message = m.FailureData.Message, MoreInfo = m.FailureData.MoreInfo }, 
                                ScheduleSendingTimeUtc = m.ScheduledSendingTimeUtc
                            })
                            .ToList(),
                    };
                    Bus.Send(coordinatorCompleteEmail);
                }
                session.SaveChanges();
            }
        }
    }
}