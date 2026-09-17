using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace FlasherCore.Common.Helpers;

public sealed class BufferedHttpStream : Stream
{
    private static readonly HttpClient _sharedClient = CreateHttpClient();

    private readonly string _url;
    private readonly long _fileLength;
    private long _position;

    private readonly int _blockSize;
    private int _concurrency;
    private readonly int _originalConcurrency;

    private class BufferBlock
    {
        public IMemoryOwner<byte> MemoryOwner;
        public Memory<byte> Data => MemoryOwner?.Memory ?? Memory<byte>.Empty;
        public long StartOffset = -1;
        public int ValidLength;
        public bool IsActive;
        public bool IsRented;
    }

    private BufferBlock _currentBuffer;
    private BufferBlock _nextBuffer;

    private Task _prefetchTask;
    private CancellationTokenSource _prefetchCts;
    private bool _isDisposed;

    private class DownloadMetrics
    {
        public long TotalBytesDownloaded;
        public int SuccessfulDownloads;
        public int FailedDownloads;
        public TimeSpan TotalDownloadTime;
        public double AverageSpeed => TotalBytesDownloaded / Math.Max(TotalDownloadTime.TotalSeconds, 0.001);
    }

    private readonly DownloadMetrics _metrics = new();
    private readonly object _metricsLock = new();

    private const int MIN_CHUNK_SIZE = 256 * 1024;
    private const int MAX_RETRIES = 3;
    private const int INITIAL_RETRY_DELAY_MS = 1000;
    private const int MAX_CONCURRENCY = 16;
    private const int DOWNLOAD_TIMEOUT_SECONDS = 30;
    private const int MIN_BYTES_PER_THREAD = 512 * 1024;
    public BufferedHttpStream(string url, long length, int blockSize = 1024 * 1024, int downloadConcurrency = 4)
    {
        if (string.IsNullOrEmpty(url)) throw new ArgumentException("URL不能为空", nameof(url));
        if (length <= 0) throw new ArgumentException("文件长度必须大于0", nameof(length));
        if (blockSize <= 0) throw new ArgumentException("块大小必须大于0", nameof(blockSize));
        if (downloadConcurrency <= 0) throw new ArgumentException("并发数必须大于0", nameof(downloadConcurrency));

        _url = url;
        _fileLength = length;
        _blockSize = blockSize;
        _concurrency = Math.Min(downloadConcurrency, MAX_CONCURRENCY);
        _originalConcurrency = _concurrency;

        var memoryPool = MemoryPool<byte>.Shared;

        _currentBuffer = new BufferBlock
        {
            MemoryOwner = memoryPool.Rent(_blockSize),
            IsRented = true
        };
        _nextBuffer = new BufferBlock
        {
            MemoryOwner = memoryPool.Rent(_blockSize),
            IsRented = true
        };
    }

    public static async Task<BufferedHttpStream> CreateAsync(string url, int blockSize = 1024 * 1024, int downloadConcurrency = 4)
    {
        if (string.IsNullOrEmpty(url)) throw new ArgumentException("URL不能为空", nameof(url));
        long length = await GetContentLengthAsync(url).ConfigureAwait(false);
        return new BufferedHttpStream(url, length, blockSize, downloadConcurrency);
    }

    private static HttpClient CreateHttpClient()
    {
        var handler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(10),
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(5),
            MaxConnectionsPerServer = 256,
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
            UseCookies = false,
            UseProxy = false,
            EnableMultipleHttp2Connections = true,
            MaxResponseHeadersLength = 64 * 1024,
            ResponseDrainTimeout = TimeSpan.FromSeconds(5),
        };

        return new HttpClient(handler)
        {
            Timeout = Timeout.InfiniteTimeSpan,
            DefaultRequestHeaders =
      {
        CacheControl = new CacheControlHeaderValue { NoCache = true },
        ConnectionClose = false
      }
        };
    }

    private static async Task<long> GetContentLengthAsync(string url)
    {
        try
        {
            var headReq = new HttpRequestMessage(HttpMethod.Head, url);
            using var headResp = await _sharedClient.SendAsync(headReq, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
            if (headResp.IsSuccessStatusCode && headResp.Content.Headers.ContentLength.HasValue)
                return headResp.Content.Headers.ContentLength.Value;
        }
        catch { }

        try
        {
            var getReq = new HttpRequestMessage(HttpMethod.Get, url);
            getReq.Headers.Range = new RangeHeaderValue(0, 0);
            using var getResp = await _sharedClient.SendAsync(getReq, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);

            if (getResp.Content.Headers.ContentRange?.Length.HasValue == true)
                return getResp.Content.Headers.ContentRange.Length.Value;

            if (getResp.Content.Headers.ContentLength.HasValue)
                return getResp.Content.Headers.ContentLength.Value;
        }
        catch (Exception ex)
        {
            throw new IOException($"无法获取文件长度: {ex.Message}", ex);
        }

        throw new IOException("无法获取文件长度：服务器未返回有效的Content-Length或Content-Range");
    }

    #region Stream 属性实现
    public override bool CanRead => true;
    public override bool CanSeek => true;
    public override bool CanWrite => false;
    public override long Length => _fileLength;
    public double DownloadSpeed => _metrics.AverageSpeed;

    public double DownloadSuccessRate
    {
        get
        {
            lock (_metricsLock)
            {
                int totalAttempts = _metrics.SuccessfulDownloads + _metrics.FailedDownloads;
                return totalAttempts > 0 ? (double)_metrics.SuccessfulDownloads / totalAttempts : 1.0;
            }
        }
    }

    public override long Position
    {
        get => _position;
        set
        {
            if (value < 0 || value > _fileLength)
                throw new ArgumentOutOfRangeException(nameof(value), $"位置必须在0到{_fileLength}之间");

            if (value != _position)
            {
                _position = value;
                if (!IsCacheHit(_currentBuffer, _position) && !IsCacheHit(_nextBuffer, _position))
                {
                    InvalidatePrefetch();
                }
            }
        }
    }
    #endregion

    public override long Seek(long offset, SeekOrigin origin)
    {
        return origin switch
        {
            SeekOrigin.Begin => Position = offset,
            SeekOrigin.Current => Position += offset,
            SeekOrigin.End => Position = _fileLength + offset,
            _ => throw new ArgumentException("无效的SeekOrigin", nameof(origin))
        };
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        return ReadAsync(buffer.AsMemory(offset, count), CancellationToken.None).AsTask().GetAwaiter().GetResult();
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (_position >= _fileLength) return 0;
        if (buffer.Length == 0) return 0;

        int totalBytesRead = 0;
        long remainingFile = _fileLength - _position;

        if (buffer.Length > remainingFile)
            buffer = buffer[..(int)remainingFile];

        while (buffer.Length > 0)
        {
            if (IsCacheHit(_currentBuffer, _position))
            {
                int read = ReadFromBuffer(_currentBuffer, buffer.Span);
                _position += read;
                totalBytesRead += read;
                buffer = buffer[read..];

                TryTriggerPrefetch(_currentBuffer.StartOffset + _blockSize);
                if (buffer.Length == 0) break;
            }

            if (IsCacheHit(_nextBuffer, _position))
            {
                if (_prefetchTask != null)
                {
                    try
                    {
                        await _prefetchTask.WaitAsync(cancellationToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch { }
                }

                if (_nextBuffer.IsActive && IsCacheHit(_nextBuffer, _position))
                {
                    (_currentBuffer, _nextBuffer) = (_nextBuffer, _currentBuffer);
                    _nextBuffer.IsActive = false;
                    continue;
                }
            }

            long blockStart = (_position / _blockSize) * _blockSize;
            long downloadLen = Math.Min(_blockSize, _fileLength - blockStart);
            var memorySlice = _currentBuffer.Data[..(int)downloadLen];

            await DownloadRangeParallelAsync(memorySlice, blockStart, (int)downloadLen, cancellationToken).ConfigureAwait(false);

            _currentBuffer.StartOffset = blockStart;
            _currentBuffer.ValidLength = (int)downloadLen;
            _currentBuffer.IsActive = true;
        }

        return totalBytesRead;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private unsafe int ReadFromBuffer(BufferBlock block, Span<byte> dest)
    {
        if (block.StartOffset == -1) return 0;

        long relativeOffset = _position - block.StartOffset;
        if (relativeOffset < 0 || relativeOffset >= block.ValidLength) return 0;

        int available = block.ValidLength - (int)relativeOffset;
        int toCopy = Math.Min(dest.Length, available);

        // 获取源和目标的内存句柄
        using var srcHandle = block.MemoryOwner.Memory.Pin();
        fixed (byte* pDest = dest)
        {
            byte* pSrc = (byte*)srcHandle.Pointer + relativeOffset;
            Buffer.MemoryCopy(pSrc, pDest, dest.Length, toCopy);
        }

        return toCopy;
    }

    private void TryTriggerPrefetch(long nextBlockStart)
    {
        if (nextBlockStart >= _fileLength) return;

        if (_nextBuffer.StartOffset == nextBlockStart &&
          (_nextBuffer.IsActive || (_prefetchTask != null && !_prefetchTask.IsCompleted)))
            return;

        long currentBufferEnd = _currentBuffer.StartOffset + _currentBuffer.ValidLength;
        long remainingInBuffer = currentBufferEnd - _position;

        if (remainingInBuffer > _blockSize / 4 && _currentBuffer.IsActive)
            return;

        _prefetchCts?.Cancel();
        _prefetchCts?.Dispose();
        _prefetchCts = new CancellationTokenSource();
        var token = _prefetchCts.Token;

        long fetchLen = Math.Min(_blockSize, _fileLength - nextBlockStart);

        _nextBuffer.StartOffset = nextBlockStart;
        _nextBuffer.ValidLength = (int)fetchLen;
        _nextBuffer.IsActive = false;

        _prefetchTask = DownloadRangeParallelAsync(
      _nextBuffer.Data[..(int)fetchLen],
      nextBlockStart,
      (int)fetchLen,
      token)
      .AsTask()
            .ContinueWith(t =>
            {
                _nextBuffer.IsActive = t.IsCompletedSuccessfully;
            }, TaskScheduler.Default);
    }

    private void InvalidatePrefetch()
    {
        _prefetchCts?.Cancel();
        _prefetchTask = null;
        _nextBuffer.IsActive = false;
        _nextBuffer.StartOffset = -1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool IsCacheHit(BufferBlock block, long reqPos)
    {
        if (block.StartOffset == -1) return false;

        ulong offset = (ulong)(reqPos - block.StartOffset);
        return offset < (ulong)block.ValidLength;
    }
    private async ValueTask DownloadRangeParallelAsync(Memory<byte> destination, long start, int count, CancellationToken token)
    {
        int effectiveConcurrency = Math.Min(_concurrency, count / MIN_BYTES_PER_THREAD);
        if (effectiveConcurrency < 1) effectiveConcurrency = 1;

        if (effectiveConcurrency == 1)
        {
            await DownloadChunkWithRetryAsync(destination, start, count, token).ConfigureAwait(false);
            return;
        }

        int partSize = count / effectiveConcurrency;

        var tasks = ArrayPool<Task>.Shared.Rent(effectiveConcurrency);
        int actualTasks = 0;

        try
        {
            long currentStart = start;
            int remaining = count;

            for (int i = 0; i < effectiveConcurrency; i++)
            {
                int chunkLength = (i == effectiveConcurrency - 1) ? remaining : partSize;

                if (chunkLength <= 0) break;

                Memory<byte> slice = destination.Slice(count - remaining, chunkLength);

                tasks[actualTasks++] = DownloadChunkWithRetryAsync(slice, currentStart, chunkLength, token).AsTask();

                currentStart += chunkLength;
                remaining -= chunkLength;
            }

            await Task.WhenAll(tasks[..actualTasks]).ConfigureAwait(false);
        }
        finally
        {
            ArrayPool<Task>.Shared.Return(tasks);
        }
    }

    private async ValueTask<int> DownloadChunkWithRetryAsync(Memory<byte> destination, long start, int count, CancellationToken token)
    {
        if (count == 0) return 0;

        int retryCount = 0;
        TimeSpan retryDelay = TimeSpan.FromMilliseconds(INITIAL_RETRY_DELAY_MS);

        var sw = Stopwatch.StartNew();

        while (retryCount < MAX_RETRIES)
        {
            try
            {
                int downloaded = await DownloadChunkAsync(destination, start, count, token).ConfigureAwait(false);

                lock (_metricsLock)
                {
                    _metrics.TotalBytesDownloaded += downloaded;
                    _metrics.SuccessfulDownloads++;
                    _metrics.TotalDownloadTime += sw.Elapsed;
                }

                AdaptConcurrency();
                return downloaded;
            }
            catch (Exception ex) when (retryCount < MAX_RETRIES - 1 &&
                        (ex is HttpRequestException || ex is IOException || ex is TaskCanceledException))
            {
                retryCount++;
                lock (_metricsLock) { _metrics.FailedDownloads++; }

                if (retryCount < MAX_RETRIES)
                    await Task.Delay(retryDelay * retryCount, token).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                lock (_metricsLock) { _metrics.FailedDownloads++; }
                throw new IOException($"下载失败: {ex.Message}", ex);
            }
        }

        throw new IOException($"下载失败: 重试{MAX_RETRIES}次后仍然失败");
    }

    private async ValueTask<int> DownloadChunkAsync(Memory<byte> destination, long start, int count, CancellationToken token)
    {
        if (count == 0) return 0;

        var request = new HttpRequestMessage(HttpMethod.Get, _url);
        request.Headers.Range = new RangeHeaderValue(start, start + count - 1);

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(DOWNLOAD_TIMEOUT_SECONDS));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(token, timeoutCts.Token);

        using var response = await _sharedClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, linkedCts.Token).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(linkedCts.Token).ConfigureAwait(false);

        int totalRead = 0;
        while (totalRead < count)
        {
            int read = await stream.ReadAsync(destination.Slice(totalRead, count - totalRead), linkedCts.Token).ConfigureAwait(false);
            if (read == 0) throw new IOException("流提前结束");
            totalRead += read;
        }

        return totalRead;
    }

    private void AdaptConcurrency()
    {
        lock (_metricsLock)
        {
            double successRate = DownloadSuccessRate;
            if (successRate < 0.8 && _concurrency > 1)
            {
                _concurrency = Math.Max(1, _concurrency - 1);
            }
            else if (successRate > 0.95 && _metrics.AverageSpeed < 50 * 1024 * 1024)
            {
                _concurrency = Math.Min(MAX_CONCURRENCY, _concurrency + 1);
            }
        }
    }

    public void ResetConcurrency() => _concurrency = _originalConcurrency;

    #region IDisposable 实现
    protected override void Dispose(bool disposing)
    {
        if (_isDisposed) return;
        _isDisposed = true;

        try
        {
            _prefetchCts?.Cancel();
            _prefetchCts?.Dispose();
            _prefetchTask = null;

            ReturnBuffer(_currentBuffer);
            ReturnBuffer(_nextBuffer);
        }
        finally
        {
            base.Dispose(disposing);
        }
    }

    private static void ReturnBuffer(BufferBlock block)
    {
        if (block is not { IsRented: true }) return;
        block.MemoryOwner?.Dispose();
        block.MemoryOwner = null;
        block.IsRented = false;
        block.StartOffset = -1;
        block.ValidLength = 0;
        block.IsActive = false;
    }

    public override async ValueTask DisposeAsync()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        _prefetchCts?.Cancel();

        if (_prefetchTask != null)
        {
            try { await _prefetchTask.ConfigureAwait(false); }
            catch { }
        }

        _prefetchCts?.Dispose();
        ReturnBuffer(_currentBuffer);
        ReturnBuffer(_nextBuffer);

        await base.DisposeAsync().ConfigureAwait(false);
    }
    #endregion

    #region 未实现的Stream方法
    public override void Flush() { }
    public override void SetLength(long value) => throw new NotSupportedException("BufferedHttpStream不支持设置长度");
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException("BufferedHttpStream是只读的");
    #endregion
}
