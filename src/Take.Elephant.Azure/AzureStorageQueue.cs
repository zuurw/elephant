﻿using Dawn;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Take.Elephant.Azure
{
    public class AzureStorageQueue<T> : IBlockingQueue<T> where T : class
    {
        private const int MIN_RECEIVE_RETRY_DELAY = 250;
        private const int MAX_RECEIVE_RETRY_DELAY = 30000;

        private readonly CloudQueue _queue;
        private readonly string _queueName;
        private readonly ISerializer<T> _serializer;
        private readonly SemaphoreSlim _queueCreationSemaphore;

        private bool _queueExists;

        public AzureStorageQueue(string storageConnectionString, string queueName, ISerializer<T> serializer, bool encodeMessage = true)
        {
            Guard.Argument(storageConnectionString).NotNull().NotEmpty();
            Guard.Argument(queueName).NotNull().NotEmpty();
            Guard.Argument(serializer).NotNull();

            _queueName = queueName;
            _serializer = serializer;
            var storageAccount = CloudStorageAccount.Parse(storageConnectionString);
            var client = storageAccount.CreateCloudQueueClient();
            _queue = client.GetQueueReference(queueName);
            _queue.EncodeMessage = encodeMessage;
            _queueCreationSemaphore = new SemaphoreSlim(1, 1);
        }

        public virtual async Task EnqueueAsync(T item, CancellationToken cancellationToken = default)
        {
            await CreateQueueIfNotExistsAsync(cancellationToken);

            var message = CreateMessage(item);
            await _queue.AddMessageAsync(message, null, null, null, null, cancellationToken);
        }

        public virtual async Task<T> DequeueAsync(CancellationToken cancellationToken)
        {
            await CreateQueueIfNotExistsAsync(cancellationToken);

            var tryCount = 0;
            var delay = MIN_RECEIVE_RETRY_DELAY;

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var item = await DequeueOrDefaultAsync(cancellationToken);
                if (item != null)
                {
                    return item;
                }

                await Task.Delay(delay, cancellationToken);
                tryCount++;

                if (delay < MAX_RECEIVE_RETRY_DELAY)
                {
                    delay = MIN_RECEIVE_RETRY_DELAY * (int)Math.Pow(2, tryCount);
                    if (delay > MAX_RECEIVE_RETRY_DELAY)
                    {
                        delay = MAX_RECEIVE_RETRY_DELAY;
                    }
                }
            }
        }

        public virtual async Task<T> DequeueOrDefaultAsync(CancellationToken cancellationToken = default)
        {
            await CreateQueueIfNotExistsAsync(cancellationToken);

#if NET461
            var message = await _queue.GetMessageAsync(cancellationToken);
#else
            var message = await _queue.GetMessageAsync(null, null, null, cancellationToken);
#endif
            if (message == null) return default(T);

            var item = CreateItem(message);

#if NET461
            await _queue.DeleteMessageAsync(message, cancellationToken);
#else
            await _queue.DeleteMessageAsync(message);
#endif

            return item;
        }

        public virtual async Task<long> GetLengthAsync(CancellationToken cancellationToken = default)
        {
            await CreateQueueIfNotExistsAsync(cancellationToken);
#if NET461
            await _queue.FetchAttributesAsync(cancellationToken);
#else
            await _queue.FetchAttributesAsync();
#endif
            return _queue.ApproximateMessageCount ?? 0;
        }

        protected virtual CloudQueueMessage CreateMessage(T item)
        {
            var serializedItem = _serializer.Serialize(item);

            return new CloudQueueMessage(serializedItem);
        }

        protected virtual T CreateItem(CloudQueueMessage message)
        {
            return _serializer.Deserialize(message.AsString);
        }

        private async Task CreateQueueIfNotExistsAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            if (_queueExists) return;

            await _queueCreationSemaphore.WaitAsync(cancellationToken);
            try
            {
                if (_queueExists) return;
#if NET461

                await _queue.CreateIfNotExistsAsync(cancellationToken);
#else
                await _queue.CreateIfNotExistsAsync(
                    null, 
                    null, 
                    cancellationToken);
#endif
                _queueExists = true;
            }
            finally
            {
                _queueCreationSemaphore.Release();
            }
        }


    }
}
