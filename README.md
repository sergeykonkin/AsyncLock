# AsyncMonitor
Inspired by and based on Stephen Cleary's AsyncEx library.
The difference is this implementation takes an external object as parameter to ```EnterAsync``` and ```Exit``` methods. This is useful when the lock object is dynamically defined at runtime.

The purpose of reinventing the wheel was a problem of accessing cacheable resources. Quick example:

```csharp
public class CachingFileReader : IFileReader
{
    private readonly IDictionary<string, byte[]> _cache;
    private readonly IFileReader _fileReader;

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
            _cache[fileName] = fileData;
            return fileData;
        }
    }
}
```

## Instalation

No NuGet for now. Feel free to just grab the single [AsyncMonitor.cs](AsyncUtil/AsyncMonitor.cs) file.