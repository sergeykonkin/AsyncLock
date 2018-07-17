using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;

namespace AsyncUtil.Test
{
    [TestFixture]
    public class AsyncMonitorTest
    {
        [Test]
        public async Task CachedResourceUseCaseTest()
        {
            // Arrange
            var fileReader = new CachingFileReader(new FileReader());

            // Act
            var tasks = new List<Task>();
            for (int i = 0; i < 10; i++)
            {
                tasks.Add(fileReader.ReadAsync("a"));
            }

            await Task.WhenAll(tasks);
            var results = tasks
                .Cast<Task<byte[]>>()
                .Select(t => t.Result)
                .Select(Convert.ToBase64String)
                .ToList();

            var unique = new HashSet<string>(results);

            var oneMore = Convert.ToBase64String(await fileReader.ReadAsync("a"));
            unique.Add(oneMore);

            // Assert
            Assert.AreEqual(1, unique.Count);
            Assert.AreEqual(1, fileReader.RealReadCallCount);
        }
    }

    public class CachingFileReader : IFileReader
    {
        private readonly IDictionary<string, byte[]> _cache;
        private readonly IFileReader _fileReader;

        public int RealReadCallCount;

        public CachingFileReader(IFileReader fileReader)
        {
            _cache = new ConcurrentDictionary<string,byte[]>();
            _fileReader = fileReader;
        }

        public async Task<byte[]> ReadAsync(string fileName)
        {
            if (_cache.ContainsKey(fileName))
                return _cache[fileName];

            using (await AsyncMonitor.EnterAsync(fileName))
            {
                // double check cache since it can be populated in another thread
                if (_cache.ContainsKey(fileName))
                    return _cache[fileName];

                var fileData = await _fileReader.ReadAsync(fileName);
                RealReadCallCount++;
                _cache[fileName] = fileData;
                return fileData;
            }
        }
    }

    public class FileReader : IFileReader
    {
        private readonly Random _random = new Random();

        public async Task<byte[]> ReadAsync(string fileName)
        {
            await Task.Delay(200);
            var result = new byte[8];
            _random.NextBytes(result);
            return result;
        }
    }

    public interface IFileReader
    {
        Task<byte[]> ReadAsync(string fileName);
    }
}
