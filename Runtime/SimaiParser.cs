using MajSimai.Utils;
using System;
using System.Buffers;
using System.IO;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace MajSimai
{
    /// <summary>
    /// Provides methods to parse simai file
    /// </summary>
    public static class SimaiParser
    {
        readonly static Task<SimaiChart> SimaiChartCompletedTask = Task.FromResult(SimaiChart.Empty);
        #region Parse
        /// <summary>
        /// Read simai text from <paramref name="content"/> and parse it into <seealso cref="SimaiFile"/>.
        /// </summary>
        /// <param name="content">Simai text</param>
        /// <returns></returns>
        public static SimaiFile Parse(ReadOnlySpan<char> content)
        {
            var metadata = ParseMetadata(content);
            return Parse(metadata);
        }
        /// <summary>
        /// Parse simai <paramref name="metadata"/> into <seealso cref="SimaiFile"/>.
        /// </summary>
        /// <param name="metadata">Simai metadata</param>
        /// <returns></returns>
        public static SimaiFile Parse(SimaiMetadata metadata)
        {
            var rentedArrayForCharts = ArrayPool<SimaiChart>.Shared.Rent(7);
            try
            {
                Parallel.For(0, 7, i =>
                {
                    var fumen = metadata.Fumens[i];
                    var designer = metadata.Designers[i];
                    var level = metadata.Levels[i];
                    try
                    {
                        rentedArrayForCharts[i] = ParseChart(level, designer, fumen);
                    }
                    catch (Exception ex)
                    {
                        rentedArrayForCharts[i] = new SimaiChart(metadata.Levels[i], metadata.Designers[i], metadata.Fumens[i], Array.Empty<SimaiTimingPoint>());
                        Console.WriteLine(ex);
                    }
                });
                var simaiFile = new SimaiFile(metadata.Title, metadata.Artist, metadata.Offset, rentedArrayForCharts, null);
                var cmds = simaiFile.Commands;
                var cmdCount = metadata.Commands.Length;
                for (var i = 0; i < cmdCount; i++)
                {
                    cmds.Add(metadata.Commands[i]);
                }

                return simaiFile;
            }
            finally
            {
                ArrayPool<SimaiChart>.Shared.Return(rentedArrayForCharts);
            }
        }
        /// <summary>
        /// Read simai text from <paramref name="contentStream"/> using UTF-8 encoding and parse it into <seealso cref="SimaiFile"/>. The Stream will be read to completion.
        /// </summary>
        /// <param name="contentStream">Provide simai content</param>
        /// <returns></returns>
        public static SimaiFile Parse(Stream contentStream)
        {
            return Parse(contentStream, Encoding.UTF8);
        }
        /// <summary>
        /// Read simai text from <paramref name="contentStream"/> using <paramref name="encoding"/> and parse it into <seealso cref="SimaiFile"/>. The Stream will be read to completion.
        /// </summary>
        /// <param name="contentStream">Provide simai content</param>
        /// <returns></returns>
        public static SimaiFile Parse(Stream contentStream, Encoding encoding)
        {
            using (var decodeStream = new StreamReader(contentStream, encoding, true, 1024, leaveOpen: true))
            {
                var contentBuffer = ArrayPool<char>.Shared.Rent(4096);
                try
                {
                    var read = 0;
                    Span<char> buffer = stackalloc char[4096];
                    while (!decodeStream.EndOfStream)
                    {
                        var currentRead = decodeStream.Read(buffer);
                        if (currentRead == 0)
                        {
                            continue;
                        }
                        BufferHelper.EnsureBufferLength(read + currentRead, ref contentBuffer);
                        buffer.Slice(0, currentRead).CopyTo(contentBuffer.AsSpan(read));
                        read += currentRead;
                    }
                    return Parse(contentBuffer.AsSpan(0, read));
                }
                finally
                {
                    ArrayPool<char>.Shared.Return(contentBuffer);
                }
            }
        }
        /// <summary>
        /// Read simai text from <paramref name="content"/> and parse it into <seealso cref="SimaiFile"/>.
        /// </summary>
        /// <param name="content">Simai text</param>
        /// <returns></returns>
        public static Task<SimaiFile> ParseAsync(string content)
        {
            var buffer = ArrayPool<char>.Shared.Rent(content.Length);
            try
            {
                content.AsSpan().CopyTo(buffer);
                return ParseAsync(buffer.AsMemory(0, content.Length));
            }
            finally
            {
                ArrayPool<char>.Shared.Return(buffer);
            }
        }
        /// <summary>
        /// Read simai text from <paramref name="content"/> and parse it into <seealso cref="SimaiFile"/>.
        /// </summary>
        /// <param name="content">Simai text</param>
        /// <returns></returns>
        public static async Task<SimaiFile> ParseAsync(ReadOnlyMemory<char> content)
        {
            var metadata = await ParseMetadataAsync(content);
            return await ParseAsync(metadata);
        }
        /// <summary>
        /// Parse simai <paramref name="metadata"/> into <seealso cref="SimaiFile"/>.
        /// </summary>
        /// <param name="metadata">Simai metadata</param>
        /// <returns></returns>
        public static async Task<SimaiFile> ParseAsync(SimaiMetadata metadata)
        {
            var rentedArrayForCharts = ArrayPool<SimaiChart>.Shared.Rent(7);
            var rentedArrayForTasks = ArrayPool<Task<SimaiChart>>.Shared.Rent(7);
            Array.Fill(rentedArrayForTasks, SimaiChartCompletedTask);
            try
            {
                for (var i = 0; i < 7; i++)
                {
                    var fumen = metadata.Fumens[i];
                    var designer = metadata.Designers[i];
                    var level = metadata.Levels[i];
                    rentedArrayForTasks[i] = ParseChartAsync(level, designer, fumen);
                }
                var tcs = new TaskCompletionSource<bool>();
                _ = Task.WhenAll(rentedArrayForTasks).ContinueWith(_ =>
                {
                    tcs.TrySetResult(true);
                });

                await tcs.Task;

                for (var i = 0; i < 7; i++)
                {
                    var task = rentedArrayForTasks[i];
                    if (task.IsCompletedSuccessfully)
                    {
                        rentedArrayForCharts[i] = task.Result;
                    }
                    else
                    {
                        rentedArrayForCharts[i] = new SimaiChart(metadata.Levels[i], metadata.Designers[i], metadata.Fumens[i], Array.Empty<SimaiTimingPoint>());
                        if (task.IsFaulted)
                        {
                            Console.WriteLine(task.Exception);
                        }
                    }
                }

                var simaiFile = new SimaiFile(metadata.Title, metadata.Artist, metadata.Offset, rentedArrayForCharts, null);
                var cmds = simaiFile.Commands;
                var cmdCount = metadata.Commands.Length;
                for (var i = 0; i < cmdCount; i++)
                {
                    cmds.Add(metadata.Commands[i]);
                }

                return simaiFile;
            }
            finally
            {
                ArrayPool<SimaiChart>.Shared.Return(rentedArrayForCharts);
                ArrayPool<Task<SimaiChart>>.Shared.Return(rentedArrayForTasks);
            }
        }
        /// <summary>
        /// Read simai text from <paramref name="contentStream"/> using UTF-8 encoding and parse it into <seealso cref="SimaiFile"/>. The Stream will be read to completion.
        /// </summary>
        /// <param name="contentStream">Provide simai content</param>
        /// <returns></returns>
        public static Task<SimaiFile> ParseAsync(Stream contentStream)
        {
            return ParseAsync(contentStream, Encoding.UTF8);
        }
        /// <summary>
        /// Read simai text from <paramref name="contentStream"/> using <paramref name="encoding"/> and parse it into <seealso cref="SimaiFile"/>. The Stream will be read to completion.
        /// </summary>
        /// <param name="contentStream">Provide simai content</param>
        /// <returns></returns>
        public static async Task<SimaiFile> ParseAsync(Stream contentStream, Encoding encoding)
        {

            using (var decodeStream = new StreamReader(contentStream, encoding, true, 1024, leaveOpen: true)) 
            {
                var contentBuffer = ArrayPool<char>.Shared.Rent(1024);
                var buffer = ArrayPool<char>.Shared.Rent(4096);
                var read = 0;
                try
                {
                    while (!decodeStream.EndOfStream)
                    {
                        var currentRead = await decodeStream.ReadAsync(buffer);
                        if (currentRead == 0)
                        {
                            continue;
                        }
                        BufferHelper.EnsureBufferLength(read + currentRead, ref contentBuffer);
                        Array.Copy(buffer, 0, contentBuffer, read, currentRead);
                        read += currentRead;
                    }
                    return await ParseAsync(contentBuffer.AsMemory(0, read));
                }
                finally
                {
                    ArrayPool<char>.Shared.Return(buffer);
                    ArrayPool<char>.Shared.Return(contentBuffer);
                }
            }
        }
        #endregion
        #region ParseMetadata
        /// <summary>
        /// Read simai text from <paramref name="content"/> and parse it into <seealso cref="SimaiMetadata"/>. SimaiNote will not be parsed.
        /// </summary>
        /// <param name="content">Simai text</param>
        /// <returns></returns>
        /// <exception cref="InvalidSimaiMarkupException"></exception>
        public static SimaiMetadata ParseMetadata(ReadOnlySpan<char> content)
        {
            static void SetValue(ReadOnlySpan<char> value, ref string valueStr)
            {
                if (!string.IsNullOrEmpty(valueStr))
                {
                    return;
                }
                else if (value.Length == 0)
                {
                    return;
                }
                valueStr = new string(value);
            }
            var title = string.Empty;
            var artist = string.Empty;
            var designer = string.Empty;
            var first = 0f;
            var rentedArrayForDesigners = ArrayPool<string>.Shared.Rent(7);
            var rentedArrayForFumens = ArrayPool<string>.Shared.Rent(7);
            var rentedArrayForLevels = ArrayPool<string>.Shared.Rent(7);

            var designers = rentedArrayForDesigners.AsSpan(0, 7);
            var fumens = rentedArrayForFumens.AsSpan(0, 7);
            var levels = rentedArrayForLevels.AsSpan(0, 7);
            var commands = ArrayPool<SimaiCommand>.Shared.Rent(16);
            var cI = 0;// for commands
            var i = 0;
            try
            {
                designers.Fill(string.Empty);
                fumens.Fill(string.Empty);
                levels.Fill(string.Empty);

                var lineCount = content.Count('\n') + 1;
                Span<Range> ranges = stackalloc Range[lineCount];
                lineCount = content.Split(ranges, '\n');
                ranges = ranges.Slice(0, lineCount);
                for (i = 0; i < lineCount; i++)
                {
                    var range = ranges[i];
                    var maidataTxt = content[range].Trim();
                    var tagIndex = maidataTxt.IndexOf('=');
                    if(maidataTxt.IsEmpty || maidataTxt[0] != '&')
                    {
                        continue;
                    }
                    else if (tagIndex == -1)
                    {
                        throw new InvalidSimaiMarkupException(i + 1, 0, maidataTxt.ToString());
                    }
                    var prefixStr = maidataTxt[1..tagIndex];
                    var valueStr = maidataTxt[(tagIndex + 1)..];
                    if (prefixStr.IsEmpty)
                    {
                        continue;
                    }
                    var prefix = new string(prefixStr);
                    var inotePrefix = 0;
                    switch (prefix)
                    {
                        case "title":
                            SetValue(valueStr, ref title);
                            break;
                        case "artist":
                            SetValue(valueStr, ref artist);
                            break;
                        case "des":
                            SetValue(valueStr, ref designer);
                            break;
                        case "des_1":
                            SetValue(valueStr, ref designers[0]);
                            break;
                        case "des_2":
                            SetValue(valueStr, ref designers[1]);
                            break;
                        case "des_3":
                            SetValue(valueStr, ref designers[2]);
                            break;
                        case "des_4":
                            SetValue(valueStr, ref designers[3]);
                            break;
                        case "des_5":
                            SetValue(valueStr, ref designers[4]);
                            break;
                        case "des_6":
                            SetValue(valueStr, ref designers[5]);
                            break;
                        case "des_7":
                            SetValue(valueStr, ref designers[6]);
                            break;
                        case "first":
                            if (!float.TryParse(valueStr, out first))
                            {
                                first = 0;
                            }
                            break;
                        case "lv_1":
                            SetValue(valueStr, ref levels[0]);
                            break;
                        case "lv_2":
                            SetValue(valueStr, ref levels[1]);
                            break;
                        case "lv_3":
                            SetValue(valueStr, ref levels[2]);
                            break;
                        case "lv_4":
                            SetValue(valueStr, ref levels[3]);
                            break;
                        case "lv_5":
                            SetValue(valueStr, ref levels[4]);
                            break;
                        case "lv_6":
                            SetValue(valueStr, ref levels[5]);
                            break;
                        case "lv_7":
                            SetValue(valueStr, ref levels[6]);
                            break;
                        case "inote_1":
                            inotePrefix = 1;
                            goto INOTE_PROCESSOR;
                        case "inote_2":
                            inotePrefix = 2;
                            goto INOTE_PROCESSOR;
                        case "inote_3":
                            inotePrefix = 3;
                            goto INOTE_PROCESSOR;
                        case "inote_4":
                            inotePrefix = 4;
                            goto INOTE_PROCESSOR;
                        case "inote_5":
                            inotePrefix = 5;
                            goto INOTE_PROCESSOR;
                        case "inote_6":
                            inotePrefix = 6;
                            goto INOTE_PROCESSOR;
                        case "inote_7":
                            inotePrefix = 7;
                            goto INOTE_PROCESSOR;
                        default:
                            BufferHelper.EnsureBufferLength(cI + 1, ref commands);
                            commands[cI++] = new SimaiCommand(prefix, new string(valueStr));
                            break;
                        INOTE_PROCESSOR:
                            {
                                if (inotePrefix == 0)
                                {
                                    throw new ArgumentOutOfRangeException();
                                }
                                var buffer = ArrayPool<char>.Shared.Rent(32);
                                try
                                {
                                    Array.Clear(buffer, 0, buffer.Length);
                                    var bufferIndex = 0;
                                    BufferHelper.EnsureBufferLength(valueStr.Length + 1, ref buffer);
                                    valueStr.CopyTo(buffer);
                                    bufferIndex += valueStr.Length;
                                    buffer[bufferIndex++] = '\n';
                                    i++;
                                    for (; i < lineCount; i++)
                                    {
                                        var isEOF = false;
                                        range = ranges[i];
                                        maidataTxt = content[range].Trim();
                                        if (maidataTxt.IsEmpty)
                                        {
                                            continue;
                                        }
                                        else if (maidataTxt[0] == '&')
                                        {
                                            isEOF = true;
                                            i--;
                                            break;
                                        }
                                        for (var i2 = 0; i2 < maidataTxt.Length; i2++)
                                        {
                                            ref readonly var current = ref maidataTxt[i2];
                                            //if (current == 'E')
                                            //{
                                            //    isEOF = true;
                                            //    break;
                                            //}
                                            BufferHelper.EnsureBufferLength(bufferIndex + 1, ref buffer);
                                            buffer[bufferIndex++] = current;
                                        }
                                        //if (isEOF)
                                        //{
                                        //    break;
                                        //}
                                        BufferHelper.EnsureBufferLength(bufferIndex + 1, ref buffer);
                                        buffer[bufferIndex++] = '\n';
                                    }

                                    fumens[inotePrefix - 1] = new string(buffer.AsSpan(0, bufferIndex).Trim());
                                }
                                finally
                                {
                                    ArrayPool<char>.Shared.Return(buffer);
                                }
                            }
                            break;
                    }
                }
                if (!string.IsNullOrEmpty(designer))
                {
                    for (var j = 0; j < 7; j++)
                    {
                        ref var d = ref designers[j];
                        if (string.IsNullOrEmpty(d))
                        {
                            d = designer;
                        }
                    }
                }
                var encoding = Encoding.UTF8;
                var byteCount = encoding.GetByteCount(content);
                var bytes = new byte[byteCount];
                encoding.GetBytes(content, bytes);
                return new SimaiMetadata(title,
                                         artist,
                                         first,
                                         designers,
                                         levels,
                                         fumens,
                                         commands.AsSpan(0, cI),
                                         MD5Helper.ComputeHashAsBase64String(bytes));
            }
            catch (InvalidSimaiMarkupException)
            {
                throw;
            }
            catch (Exception e)
            {
                throw new InvalidSimaiMarkupException(i + 1, 0, "在maidata.txt第" + (i + 1) + "行:\n" + e.Message + "读取谱面时出现错误");
            }
            finally
            {
                ArrayPool<string>.Shared.Return(rentedArrayForDesigners);
                ArrayPool<string>.Shared.Return(rentedArrayForFumens);
                ArrayPool<string>.Shared.Return(rentedArrayForLevels);
                ArrayPool<SimaiCommand>.Shared.Return(commands);
            }
        }
        /// <summary>
        /// Read simai text from <paramref name="contentStream"/> using UTF-8 encoding and parse it into <seealso cref="SimaiMetadata"/>. SimaiNote will not be parsed.
        /// </summary>
        /// <param name="contentStream">Provide simai content</param>
        /// <returns></returns>
        /// <exception cref="InvalidSimaiMarkupException"></exception>
        public static SimaiMetadata ParseMetadata(Stream contentStream)
        {
            return ParseMetadata(contentStream, Encoding.UTF8);
        }
        /// <summary>
        /// Read simai text from <paramref name="contentStream"/> using <paramref name="encoding"/> and parse it into <seealso cref="SimaiMetadata"/>. SimaiNote will not be parsed. 
        /// <para>The Stream will be read to completion.</para>
        /// </summary>
        /// <param name="contentStream">Provide simai content</param>
        /// <returns></returns>
        /// <exception cref="InvalidSimaiMarkupException"></exception>
        public static SimaiMetadata ParseMetadata(Stream contentStream, Encoding encoding)
        {
            using (var decodeStream = new StreamReader(contentStream, encoding, true, 1024, leaveOpen: true))
            {
                var contentBuffer = ArrayPool<char>.Shared.Rent(4096);
                try
                {
                    var read = 0;
                    Span<char> buffer = stackalloc char[4096];
                    while (!decodeStream.EndOfStream)
                    {
                        var currentRead = decodeStream.Read(buffer);
                        if (currentRead == 0)
                        {
                            continue;
                        }
                        BufferHelper.EnsureBufferLength(read + currentRead, ref contentBuffer);
                        buffer.Slice(0, currentRead).CopyTo(contentBuffer.AsSpan(read));
                        read += currentRead;
                    }
                    return ParseMetadata(contentBuffer.AsSpan(0, read));
                }
                finally
                {
                    ArrayPool<char>.Shared.Return(contentBuffer);
                }
            }
        }
        /// <summary>
        /// Read simai text from <paramref name="content"/> and parse it into <seealso cref="SimaiMetadata"/>. SimaiNote will not be parsed.
        /// <para>The Stream will be read to completion.</para>
        /// </summary>
        /// <param name="content">Simai text</param>
        /// <returns></returns>
        /// <exception cref="InvalidSimaiMarkupException"></exception>
        public static Task<SimaiMetadata> ParseMetadataAsync(string content)
        {
            return Task.Run(() => ParseMetadata(content));
        }
        /// <summary>
        /// Read simai text from <paramref name="content"/> and parse it into <seealso cref="SimaiMetadata"/>. SimaiNote will not be parsed.
        /// </summary>
        /// <param name="content">Simai text</param>
        /// <returns></returns>
        /// <exception cref="InvalidSimaiMarkupException"></exception>
        public static Task<SimaiMetadata> ParseMetadataAsync(ReadOnlyMemory<char> content)
        {
            return Task.Run(() => ParseMetadata(content.Span));
        }
        /// <summary>
        /// Read simai text from <paramref name="contentStream"/> using UTF-8 encoding and parse it into <seealso cref="SimaiMetadata"/>. SimaiNote will not be parsed.
        /// <para>The Stream will be read to completion.</para>
        /// </summary>
        /// <param name="contentStream">Provide simai content</param>
        /// <returns></returns>
        /// <exception cref="InvalidSimaiMarkupException"></exception>
        public static Task<SimaiMetadata> ParseMetadataAsync(Stream contentStream)
        {
            return ParseMetadataAsync(contentStream, Encoding.UTF8);
        }
        /// <summary>
        /// Read simai text from <paramref name="contentStream"/> using <paramref name="encoding"/> and parse it into <seealso cref="SimaiMetadata"/>. SimaiNote will not be parsed. 
        /// <para>The Stream will be read to completion.</para>
        /// </summary>
        /// <param name="contentStream">Provide simai content</param>
        /// <returns></returns>
        /// <exception cref="InvalidSimaiMarkupException"></exception>
        public static async Task<SimaiMetadata> ParseMetadataAsync(Stream contentStream, Encoding encoding)
        {
            using (var decodeStream = new StreamReader(contentStream, encoding, true, 1024, leaveOpen: true))
            {
                var contentBuffer = ArrayPool<char>.Shared.Rent(4096);
                var buffer = ArrayPool<char>.Shared.Rent(4096);
                try
                {
                    var read = 0;
                    while (!decodeStream.EndOfStream)
                    {
                        var currentRead = await decodeStream.ReadAsync(buffer);
                        if (currentRead == 0)
                        {
                            continue;
                        }
                        BufferHelper.EnsureBufferLength(read + currentRead, ref contentBuffer);
                        Array.Copy(buffer, 0, contentBuffer, read, currentRead);
                        read += currentRead;
                    }
                    return await ParseMetadataAsync(contentBuffer.AsMemory(0, read));
                }
                finally
                {
                    ArrayPool<char>.Shared.Return(contentBuffer);
                    ArrayPool<char>.Shared.Return(buffer);
                }
            }
        }
        #endregion
        #region ParseChart
        /// <summary>
        /// Read simai chart from <paramref name="fumen"/> and parse it into <seealso cref="SimaiChart"/>
        /// </summary>
        /// <param name="fumen">Simai chart</param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SimaiChart ParseChart(ReadOnlySpan<char> fumen)
        {
            return ParseChart(string.Empty, string.Empty, fumen);
        }
        /// <summary>
        /// Read simai chart from <paramref name="fumen"/> and parse it into <seealso cref="SimaiChart"/>
        /// </summary>
        /// <param name="level">Level of simai chart</param>
        /// <param name="designer">designer of simai chart</param>
        /// <param name="fumen">Simai chart</param>
        /// <returns></returns>
        public static SimaiChart ParseChart(string level, string designer, ReadOnlySpan<char> fumen)
        {
            static bool IsNote(char c)
            {
                var isTapOrHoldOrSlide = c >= '0' && c <= '9';
                var isTouchOrTouchHold = c >= 'A' && c <= 'E';

                return isTapOrHoldOrSlide || isTouchOrTouchHold;
            }
            if (fumen.IsEmpty)
            {
                return new SimaiChart(level, designer, string.Empty, null);
            }
            var noteContentBuffer = ArrayPool<char>.Shared.Rent(16);
            var noteRawTimingBuffer = ArrayPool<SimaiRawTimingPoint>.Shared.Rent(16);
            var commaTimingBuffer = ArrayPool<SimaiTimingPoint>.Shared.Rent(16);

            var noteContentBufIndex = 0;
            var noteRawTimingBufIndex = 0;
            var commaTimingBufIndex = 0;

            float bpm = 0;
            var curHSpeed = 1f;
            var curSVeloc = 1f;
            double time = 0; //in seconds
            var beats = 4f; //{4}
            var haveNote = false;
            var haveSV = false;
            //var noteTemp = "";

            int Ycount = 1, Xcount = 0;

            /// Xcount| 1 2 3 4 5 6 7 8 9 10| 
            /// --------------------------------
            ///       | A B C D E F G H I J | 1
            ///       | K L N M O P Q R F T | 2
            ///       | U V W X Y Z 1 1 4 5 | 3
            ///       | 1 4 X M M C G H H H | 4
            /// ----------------------------| Ycount
            try
            {
                for (var i = 0; i < fumen.Length; i++)
                {
                    if (fumen[i] == '\n')
                    {
                        Ycount++;
                        Xcount = 0;
                        continue;
                    }
                    else
                    {
                        Xcount++;
                    }
                    switch (fumen[i])
                    {
                        case '|': // 跳过注释
                            {
                                var str = fumen[i..];
                                if (str.Length >= 2 && str[1] == '|')
                                {
                                    i += 2;
                                    Xcount += 2;
                                    for (; i < fumen.Length; i++)
                                    {
                                        if (fumen[i] == '\n')
                                        {
                                            Ycount++;
                                            Xcount = 0;
                                            break;
                                        }
                                        Xcount++;
                                    }
                                }
                                else
                                {
                                    var s = fumen[i].ToString();
                                    throw new InvalidSimaiMarkupException(Ycount, Xcount, s, $"Unexpected character \"{s}\"");
                                }
                            }
                            continue;
                        case '(': //Get bpm
                            {
                                haveNote = false;
                                //noteTemp = "";
                                noteContentBufIndex = 0;

                                if (fumen[i..].Length >= 3) // (x)
                                {
                                    var startAt = i + 1;
                                    i++;
                                    Xcount++;
                                    for (; i < fumen.Length; i++)
                                    {
                                        if (fumen[i] == '\n')
                                        {
                                            Ycount++;
                                            Xcount = 0;
                                            continue;
                                        }
                                        else if (fumen[i] == ')')
                                        {
                                            break;
                                        }
                                        Xcount++;
                                    }
                                    var endAt = i;
                                    var bpmStr = fumen[startAt..endAt].Trim();

                                    if (!float.TryParse(bpmStr, out bpm))
                                    {
                                        throw new InvalidSimaiSyntaxException(Ycount, Xcount, bpmStr.ToString(), "BPM value must be a number");
                                    }
                                }
                                else
                                {
                                    var s = fumen[i].ToString();
                                    throw new InvalidSimaiMarkupException(Ycount, Xcount, s, $"Unexpected character \"{s}\"");
                                }
                                //Console.WriteLine("BPM" + bpm);
                            }
                            continue;
                        case '{'://Get beats
                            {
                                haveNote = false;
                                //noteTemp = "";
                                noteContentBufIndex = 0;

                                if (fumen[i..].Length >= 3) // {x}
                                {
                                    var startAt = i + 1;
                                    i++;
                                    Xcount++;
                                    for (; i < fumen.Length; i++)
                                    {
                                        if (fumen[i] == '\n')
                                        {
                                            Ycount++;
                                            Xcount = 0;
                                            continue;
                                        }
                                        else if (fumen[i] == '}')
                                        {
                                            break;
                                        }
                                        Xcount++;
                                    }
                                    var endAt = i;
                                    var beatsStr = fumen[startAt..endAt].Trim();

                                    if (beatsStr.IsEmpty)
                                    {
                                        throw new InvalidSimaiSyntaxException(Ycount, Xcount, fumen[startAt..endAt].ToString(), "Beats value must be a number");
                                    }
                                    else if (beatsStr[0] == '#')
                                    {
                                        if (!float.TryParse(beatsStr[1..], out var beatInterval))
                                        {
                                            throw new InvalidSimaiSyntaxException(Ycount, Xcount, beatsStr.ToString(), "Beats value must be a number");
                                        }
                                        beats = 240f / (bpm * beatInterval);
                                    }
                                    else if (!float.TryParse(beatsStr, out beats))
                                    {
                                        throw new InvalidSimaiSyntaxException(Ycount, Xcount, beatsStr.ToString(), "Beats value must be a number");
                                    }
                                }
                                else
                                {
                                    var s = fumen[i].ToString();
                                    throw new InvalidSimaiMarkupException(Ycount, Xcount, s, $"Unexpected character \"{s}\"");
                                }
                                //Console.WriteLine("BEAT" + beats);
                            }
                            continue;
                        case '<':// Get HS: <HS*1.0> / Get SV: <SV*0.5>
                            {
                                if (haveNote)
                                {
                                    break;
                                }
                                haveNote = false;
                                //noteTemp = "";
                                noteContentBufIndex = 0;

                                if (fumen[i..].Length >= 4) // <HS*x>
                                {
                                    var startAt = i + 1;
                                    var buffer = ArrayPool<char>.Shared.Rent(16);
                                    var bufferIndex = 0;
                                    var tagIndex = -1; // position of '*'
                                    i++;
                                    Xcount++;
                                    try
                                    {
                                        for (; i < fumen.Length; i++)
                                        {
                                            ref readonly var currentChar = ref fumen[i];
                                            if (currentChar == '\n')
                                            {
                                                Ycount++;
                                                Xcount = 0;
                                                continue;
                                            }
                                            else if (currentChar == '*')
                                            {
                                                if (tagIndex != -1)
                                                {
                                                    throw new InvalidSimaiSyntaxException(Ycount, Xcount, fumen[(startAt - 1)..(i + 1)].ToString(), "Unexpected HS declaration syntax");
                                                }
                                                tagIndex = bufferIndex;
                                            }
                                            else if (currentChar == '>')
                                            {
                                                break;
                                            }
                                            BufferHelper.EnsureBufferLength(bufferIndex + 1, ref buffer);
                                            buffer[bufferIndex++] = currentChar;
                                            Xcount++;
                                        }
                                        var Content = buffer.AsSpan(0, bufferIndex);
                                        var isInvalid = Content.IsEmpty ||
                                                        Content.Length < 4 ||
                                                        tagIndex == -1;
                                        if (!isInvalid) // min: HS*1
                                        {
                                            var Value = Content[(tagIndex + 1)..]; // get "1.0" from HS*1.0
                                            if (Content[0..tagIndex].SequenceEqual("HS"))
                                            {
                                                if (!float.TryParse(Value, out curHSpeed))
                                                {
                                                    throw new InvalidSimaiMarkupException(Ycount, Xcount, Content.ToString(), "HSpeed value must be a number");
                                                }
                                            }
                                            else if (Content[0..tagIndex].SequenceEqual("SV"))
                                            {
                                                if (float.TryParse(Value, out curSVeloc))
                                                    haveSV = true;
                                                else throw new InvalidSimaiMarkupException(Ycount, Xcount, Content.ToString(), "SVeloc value must be a number");
                                            }
                                            else throw new InvalidSimaiSyntaxException(Ycount, Xcount, Content.ToString(), $"Unexpected HS / SV declaration syntax \"{Content[0..tagIndex].ToString()}\", is it \"HS\" or \"SV\"?");
                                        }
                                        else throw new InvalidSimaiSyntaxException(Ycount, Xcount, Content.ToString(), "Unexpected HS / SV declaration syntax");

                                        //Console.WriteLine("HS" + curHSpeed);
                                    }
                                    finally
                                    {
                                        ArrayPool<char>.Shared.Return(buffer);
                                    }
                                }
                                else
                                {
                                    var s = fumen[i].ToString();
                                    throw new InvalidSimaiMarkupException(Ycount, Xcount, s, $"Unexpected character \"{s}\"");
                                }
                            }
                            continue;
                    }

                    if (!haveNote && IsNote(fumen[i]))
                    {
                        haveNote = true;
                        noteContentBufIndex = 0;
                    }

                    if (fumen[i] == ',')
                    {
                        if (haveNote || haveSV)
                        {
                            var noteContent = (ReadOnlySpan<char>)(noteContentBuffer.AsSpan(0, noteContentBufIndex));
                            var fakeEachTagCount = noteContent.Count('`');

                            if (fakeEachTagCount != 0)
                            {
                                var rentedBufferForRanges = ArrayPool<Range>.Shared.Rent(fakeEachTagCount + 1);
                                var ranges = rentedBufferForRanges.AsSpan(fakeEachTagCount + 1);
                                try
                                {
                                    // 伪双
                                    var tagCount = noteContent.Split(ranges, '`', StringSplitOptions.RemoveEmptyEntries);
                                    var fakeTime = time;
                                    var timeInterval = 1.875 / bpm; // 128分音

                                    for (var j = 0; j < tagCount; j++)
                                    {
                                        var fakeEachGroup = noteContent[ranges[j]];
                                        //Console.WriteLine(fakeEachGroup.ToString());
                                        var rawTp = new SimaiRawTimingPoint(fakeTime,
                                                                            fakeEachGroup,
                                                                            Xcount,
                                                                            Ycount,
                                                                            bpm,
                                                                            curHSpeed,
                                                                            curSVeloc);
                                        BufferHelper.EnsureBufferLength(noteRawTimingBufIndex + 1, ref noteRawTimingBuffer);
                                        noteRawTimingBuffer[noteRawTimingBufIndex++] = rawTp;
                                        fakeTime += timeInterval;
                                    }
                                }
                                finally
                                {
                                    ArrayPool<Range>.Shared.Return(rentedBufferForRanges);
                                }
                            }
                            else
                            {
                                var rawTp = new SimaiRawTimingPoint(time,
                                                                    noteContent,
                                                                    Xcount,
                                                                    Ycount,
                                                                    bpm,
                                                                    curHSpeed,
                                                                    curSVeloc);
                                BufferHelper.EnsureBufferLength(noteRawTimingBufIndex + 1, ref noteRawTimingBuffer);
                                noteRawTimingBuffer[noteRawTimingBufIndex++] = rawTp;
                            }
                            //Console.WriteLine("Note:" + noteTemp);

                            //noteTemp = "";
                            noteContentBufIndex = 0;
                        }
                        BufferHelper.EnsureBufferLength(commaTimingBufIndex + 1, ref commaTimingBuffer);
                        commaTimingBuffer[commaTimingBufIndex++] = new SimaiTimingPoint(time, null, string.Empty, Xcount, Ycount, bpm, 1, curSVeloc, i);

                        time += 1d / (bpm / 60d) * 4d / beats;
                        //Console.WriteLine(time);
                        haveNote = false;
                        noteContentBufIndex = 0;
                    }
                    else if (haveNote)
                    {
                        ref readonly var curChar = ref fumen[i];
                        BufferHelper.EnsureBufferLength(noteContentBufIndex + 1, ref noteContentBuffer);
                        noteContentBuffer[noteContentBufIndex++] = curChar;
                    }
                }
                var noteTimingPoints = new SimaiTimingPoint[noteRawTimingBufIndex];

                Parallel.For(0, noteRawTimingBufIndex, i =>
                {
                    var rawTiming = noteRawTimingBuffer[i];
                    var timingPoint = rawTiming.Parse();
                    noteTimingPoints[i] = timingPoint;
                });

                return new SimaiChart(level, 
                                      designer, 
                                      fumen.ToString(), 
                                      noteTimingPoints.AsSpan(0, noteRawTimingBufIndex), 
                                      commaTimingBuffer.AsSpan(0, commaTimingBufIndex));
            }
            catch (InvalidSimaiMarkupException)
            {
                throw;
            }
            catch (Exception e)
            {
                throw new Exception("Error at " + Ycount + "," + Xcount + "\n" + e.Message);
            }
            finally
            {
                ArrayPool<char>.Shared.Return(noteContentBuffer);
                ArrayPool<SimaiRawTimingPoint>.Shared.Return(noteRawTimingBuffer);
                ArrayPool<SimaiTimingPoint>.Shared.Return(commaTimingBuffer);
            }
        }
        /// <summary>
        /// Read simai chart from <paramref name="fumen"/> and parse it into <seealso cref="SimaiChart"/>
        /// </summary>
        /// <param name="fumen">Simai chart</param>
        /// <returns></returns>
        public static Task<SimaiChart> ParseChartAsync(string fumen)
        {
            return Task.Run(() => ParseChart(string.Empty, string.Empty, fumen));
        }
        /// <summary>
        /// Read simai chart from <paramref name="fumen"/> and parse it into <seealso cref="SimaiChart"/>
        /// </summary>
        /// <param name="fumen">Simai chart</param>
        /// <returns></returns>
        public static Task<SimaiChart> ParseChartAsync(ReadOnlyMemory<char> fumen)
        {
            return Task.Run(() => ParseChart(string.Empty, string.Empty, fumen.Span));
        }
        /// <summary>
        /// Read simai chart from <paramref name="fumen"/> and parse it into <seealso cref="SimaiChart"/>
        /// </summary>
        /// <param name="level">Level of simai chart</param>
        /// <param name="designer">designer of simai chart</param>
        /// <param name="fumen">Simai chart</param>
        /// <returns></returns>
        public static Task<SimaiChart> ParseChartAsync(string level, string designer, ReadOnlyMemory<char> fumen)
        {
            return Task.Run(() => ParseChart(level, designer, fumen.Span));
        }
        /// <summary>
        /// Read simai chart from <paramref name="fumen"/> and parse it into <seealso cref="SimaiChart"/>
        /// </summary>
        /// <param name="level">Level of simai chart</param>
        /// <param name="designer">designer of simai chart</param>
        /// <param name="fumen">Simai chart</param>
        /// <returns></returns>
        public static Task<SimaiChart> ParseChartAsync(string level, string designer, string fumen)
        {
            return Task.Run(() => ParseChart(level, designer, fumen));
        }
        #endregion
        #region Deparse
        //Note: this method only deparse RawChart
        public static string Deparse(SimaiFile simaiFile)
        {
            var sb = new StringBuilder();
            var finalDesigner = string.Empty;

            sb.Append($"&title=")
              .AppendLine(simaiFile.Title)
              .Append($"&artist=")
              .AppendLine(simaiFile.Artist)
              .Append("&first=")
              .Append(simaiFile.Offset)
              .AppendLine();
            for (int i = 0; i < 7; i++)
            {
                var chart = simaiFile.Charts[i];
                if (chart is null)
                {
                    continue;
                }
                if (!string.IsNullOrEmpty(chart.Designer))
                {
                    finalDesigner = chart.Designer;
                    sb.Append("&des_")
                      .Append(i + 1)
                      .Append('=')
                      .AppendLine(chart.Designer);
                }
                if (!string.IsNullOrEmpty(chart.Level))
                {
                    sb.Append("&lv_")
                      .Append(i + 1)
                      .Append('=')
                      .AppendLine(chart.Level);
                }
            }
            sb.Append("&des_")
              .Append('=')
              .AppendLine(finalDesigner);
            foreach (var command in simaiFile.Commands)
            {
                sb.Append('&')
                  .Append(command.Prefix)
                  .Append('=')
                  .AppendLine(command.Value);
            }
            for (int i = 0; i < 7; i++)
            {
                var chart = simaiFile.Charts[i].Fumen;
                if (string.IsNullOrEmpty(chart))
                {
                    continue;
                }
                sb.Append("&inote_")
                  .Append(i + 1)
                  .Append('=')
                  .Append(chart)
                  .AppendLine()
                  .Append('E')
                  .AppendLine();
            }
            return sb.ToString();
        }
        public static void Deparse(SimaiFile simaiFile, Stream stream)
        {
            Deparse(simaiFile, stream, Encoding.UTF8);
        }
        public static void Deparse(SimaiFile simaiFile, Stream stream, Encoding encoding)
        {
            var fumen = Deparse(simaiFile);
            using var writer = new StreamWriter(stream, encoding);
            writer.Write(fumen);
        }
        public static Task<string> DeparseAsync(SimaiFile simaiFile)
        {
            return Task.Run(() => Deparse(simaiFile));
        }
        public static async Task DeparseAsync(SimaiFile simaiFile, Stream stream)
        {
            await DeparseAsync(simaiFile, stream, Encoding.UTF8);
        }
        public static async Task DeparseAsync(SimaiFile simaiFile, Stream stream, Encoding encoding)
        {
            var fumen = await DeparseAsync(simaiFile);
            using var writer = new StreamWriter(stream, encoding);
            await writer.WriteAsync(fumen);
        }
        #endregion
        static class MD5Helper
        {
            public static byte[] ComputeHash(byte[] data)
            {
                using (var md5 = MD5.Create())
                {
                    return md5.ComputeHash(data);
                }
            }
            public static string ComputeHashAsBase64String(byte[] data)
            {
                var hash = ComputeHash(data);

                return Convert.ToBase64String(hash);
            }
            public static async Task<byte[]> ComputeHashAsync(byte[] data)
            {
                using (var md5 = MD5.Create())
                {
                    return await Task.Run(() => md5.ComputeHash(data));
                }
            }
            public static async Task<string> ComputeHashAsBase64StringAsync(byte[] data)
            {
                var hash = await ComputeHashAsync(data);

                return Convert.ToBase64String(hash);
            }
        }
    }
}
