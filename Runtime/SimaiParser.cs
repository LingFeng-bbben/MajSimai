using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace MajSimai
{
    public partial class SimaiParser
    {
        public static SimaiParser Shared { get; } = new SimaiParser();
        static ReadOnlyMemory<string> SIMAI_METADATA_MARKUP = new string[]
        {
            "title",
            "artist",
            "first",
            "des",
            "des_1",
            "des_2",
            "des_3",
            "des_4",
            "des_5",
            "des_6",
            "des_7",
            "lv_1",
            "lv_2",
            "lv_3",
            "lv_4",
            "lv_5",
            "lv_6",
            "lv_7",
            "inote_1",
            "inote_2",
            "inote_3",
            "inote_4",
            "inote_5",
            "inote_6",
            "inote_7",
        };

        const int SIMAI_TITLE_MARKUP_INDEX = 0;
        const int SIMAI_ARTIST_MARKUP_INDEX = 1;
        const int SIMAI_FIRST_MARKUP_INDEX = 2;
        const int SIMAI_DES_MARKUP_INDEX = 3;
        const int SIMAI_DES_SEG_MARKUP_START_INDEX = 4;
        const int SIMAI_LEVEL_SEG_MARKUP_START_INDEX = 11;
        const int SIMAI_FUMEN_SEG_MARKUP_START_INDEX = 18;
        public async Task<SimaiFile> ParseAsync(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"\"{filePath}\" could not be found");

            using var fileStream = File.OpenRead(filePath);
            using var memoryBuffer = new MemoryStream();
            await fileStream.CopyToAsync(memoryBuffer);

            var fileContent = Encoding.UTF8.GetString(memoryBuffer.ToArray());
            var metadata = await ReadMetadataAsync(fileContent);

            SimaiChart[] charts = new SimaiChart[7];
            Task<SimaiChart>[] tasks = new Task<SimaiChart>[7];
            for (var i = 0; i < 7; i++)
            {
                var fumen = metadata.Fumens[i];
                var designer = metadata.Designers[i];
                var level = metadata.Levels[i];
                tasks[i] = ParseChartAsync(level, designer, fumen);
            }
            var allTask = Task.WhenAll(tasks);
            while (!allTask.IsCompleted)
                await Task.Yield();
            for(var i = 0; i < 7; i++)
            {
                var task = tasks[i];
                if (task.IsFaulted)
                    charts[i] = new SimaiChart(metadata.Levels[i], metadata.Designers[i], Array.Empty<SimaiTimingPoint>());
                else
                    charts[i] = task.Result;
            }
            return new SimaiFile(filePath, metadata.Title, metadata.Artist, metadata.Offset, charts, metadata.Fumens, metadata.Commands);
        }
        public async Task<SimaiMetadata> ParseMetadataAsync(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"\"{filePath}\" could not be found");

            using var fileStream = File.OpenRead(filePath);
            using var memoryBuffer = new MemoryStream();
            await fileStream.CopyToAsync(memoryBuffer);

            var fileContent = Encoding.UTF8.GetString(memoryBuffer.ToArray());
            var metadata = await ReadMetadataAsync(fileContent);

            return metadata;
        }
        public SimaiFile Parse(string filePath)
        {
            return ParseAsync(filePath).Result;
        }
        public SimaiMetadata ParseMetadata(string filePath)
        {
            return ParseMetadataAsync(filePath).Result;
        }
        public async Task<SimaiChart> ParseChartAsync(string level, string designer, string fumen)
        {
            return await Task.Run(async () =>
            {
                if (string.IsNullOrEmpty(fumen))
                {
                    return new SimaiChart(level, designer, null);
                }
                var noteRawTiminglist = new List<SimaiRawTimingPoint>();
                var commaTimingList = new List<SimaiTimingPoint>();

                float bpm = 0;
                var curHSpeed = 1f;
                double time = 0; //in seconds
                var beats = 4f;
                var haveNote = false;
                var noteTemp = "";
                int Ycount = 0, Xcount = 0;

                try
                {
                    for (var i = 0; i < fumen.Length; i++)
                    {
                        if (fumen[i] == '|' && i + 1 < fumen.Length && fumen[i + 1] == '|')
                        {
                            // 跳过注释
                            Xcount++;
                            while (i < fumen.Length && fumen[i] != '\n')
                            {
                                i++;
                                Xcount++;
                            }

                            Ycount++;
                            Xcount = 0;
                            continue;
                        }

                        if (fumen[i] == '\n')
                        {
                            Ycount++;
                            Xcount = 0;
                        }
                        else
                        {
                            Xcount++;
                        }

                        if (fumen[i] == '(')
                        //Get bpm
                        {
                            haveNote = false;
                            noteTemp = "";
                            var bpm_s = "";
                            i++;
                            Xcount++;
                            while (fumen[i] != ')')
                            {
                                bpm_s += fumen[i];
                                i++;
                                Xcount++;
                            }

                            if (!float.TryParse(bpm_s, out bpm))
                            {
                                throw new InvalidValueException(Ycount, bpm_s, "BPM value must be a floating point number");
                            }
                            //Console.WriteLine("BPM" + bpm);
                            continue;
                        }

                        if (fumen[i] == '{')
                        //Get beats
                        {
                            haveNote = false;
                            noteTemp = "";
                            var beats_s = "";
                            i++;
                            Xcount++;
                            while (fumen[i] != '}')
                            {
                                beats_s += fumen[i];
                                i++;
                                Xcount++;
                            }

                            if (!float.TryParse(beats_s, out beats))
                            {
                                throw new InvalidValueException(Ycount, beats_s, "Beats value must be a floating point number");
                            }
                            //Console.WriteLine("BEAT" + beats);
                            continue;
                        }

                        if (fumen[i] == 'H')
                        //Get HS
                        {
                            haveNote = false;
                            noteTemp = "";
                            var hs_s = "";
                            if (fumen[i + 1] == 'S' && fumen[i + 2] == '*')
                            {
                                i += 3;
                                Xcount += 3;
                            }

                            while (fumen[i] != '>')
                            {
                                hs_s += fumen[i];
                                i++;
                                Xcount++;
                            }

                            if (!float.TryParse(hs_s, out curHSpeed))
                            {
                                throw new InvalidValueException(Ycount, hs_s, "HSpeed value must be a floating point number");
                            }
                            //Console.WriteLine("HS" + curHSpeed);
                            continue;
                        }

                        if (IsNote(fumen[i])) haveNote = true;
                        if (haveNote && fumen[i] != ',') noteTemp += fumen[i];
                        if (fumen[i] == ',')
                        {
                            if (haveNote)
                            {
                                if (noteTemp.Contains('`'))
                                {
                                    // 伪双
                                    var fakeEachList = noteTemp.Split('`');
                                    var fakeTime = time;
                                    var timeInterval = 1.875 / bpm; // 128分音
                                    foreach (var fakeEachGroup in fakeEachList)
                                    {
                                        Console.WriteLine(fakeEachGroup);
                                        noteRawTiminglist.Add(new SimaiRawTimingPoint(fakeTime, Xcount, Ycount, fakeEachGroup, bpm,
                                            curHSpeed));
                                        fakeTime += timeInterval;
                                    }
                                }
                                else
                                {
                                    noteRawTiminglist.Add(new SimaiRawTimingPoint(time, Xcount, Ycount, noteTemp, bpm, curHSpeed));
                                }
                                //Console.WriteLine("Note:" + noteTemp);

                                noteTemp = "";
                            }

                            commaTimingList.Add(new SimaiTimingPoint(time, null, Xcount, Ycount, "", bpm, 1,i));


                            time += 1d / (bpm / 60d) * 4d / beats;
                            //Console.WriteLine(time);
                            haveNote = false;
                        }
                    }
                    var noteTimingPoints = new SimaiTimingPoint[noteRawTiminglist.Count];
                    for (int i = 0; i < noteRawTiminglist.Count; i++)
                    {
                        var rawTiming = noteRawTiminglist[i];
                        var timingPoint = await rawTiming.ParseAsync();
                        noteTimingPoints[i] = timingPoint;
                    }

                    return new SimaiChart(level, designer, noteTimingPoints,commaTimingList.ToArray());
                }
                catch (InvalidValueException)
                {
                    throw;
                }
                catch (Exception e)
                {
                    throw new Exception("Error at " + Ycount + "," + Xcount + "\n" + e.Message);
                }
            });
        }

        private async Task<SimaiMetadata> ReadMetadataAsync(string content)
        {
            return await Task.Run(async () =>
            {
                var title = "";
                var artist = "";
                var designer = "";
                var first = 0f;
                var designers = new string[7];
                var fumens = new string[7];
                var levels = new string[7];
                var i = 0;
                List<SimaiCommand> commands = new List<SimaiCommand>();
                try
                {
                    var maidataTxt = content.Split("\n");
                    for (i = 0; i < maidataTxt.Length; i++)
                    {
                        if (maidataTxt[i].StartsWith("&title="))
                            title = GetValue(maidataTxt[i]);
                        else if (maidataTxt[i].StartsWith("&artist="))
                            artist = GetValue(maidataTxt[i]);
                        else if (maidataTxt[i].StartsWith("&des"))
                        {
                            if (maidataTxt[i].StartsWith("&des="))
                            {
                                designer = GetValue(maidataTxt[i]);
                            }
                            else
                            {
                                for (var j = 0; j < 7; j++)
                                {
                                    if (maidataTxt[i].StartsWith($"&des_{j + 1}="))
                                        designers[j] = GetValue(maidataTxt[i]);
                                }
                            }

                        }
                        else if (maidataTxt[i].StartsWith("&first="))
                        {
                            if (!float.TryParse(GetValue(maidataTxt[i]), out first))
                            {
                                first = 0;
                            }
                        }
                        else if (maidataTxt[i].StartsWith("&lv_") || maidataTxt[i].StartsWith("&inote_"))
                        {
                            for (var j = 1; j < 8 && i < maidataTxt.Length; j++)
                            {
                                if (maidataTxt[i].StartsWith("&lv_" + j + "="))
                                    levels[j - 1] = GetValue(maidataTxt[i]);
                                if (maidataTxt[i].StartsWith("&inote_" + j + "="))
                                {
                                    var TheNote = "";
                                    TheNote += GetValue(maidataTxt[i]) + "\n";
                                    i++;
                                    for (; i < maidataTxt.Length; i++)
                                    {
                                        if (i < maidataTxt.Length)
                                            if (maidataTxt[i].StartsWith("&"))
                                                break;
                                        TheNote += maidataTxt[i] + "\n";
                                    }

                                    fumens[j - 1] = TheNote.Trim();
                                }
                            }
                        }
                        else if (maidataTxt[i].StartsWith("&"))
                        {
                            if (!maidataTxt[i].Contains("="))
                                throw new InvalidSimaiMarkupException(i + 1, maidataTxt[i]);
                            var a = maidataTxt[i].Split("=");
                            var prefix = a[0][1..];
                            var value = a[1];
                            commands.Add(new SimaiCommand(prefix, value));
                        }
                    }
                    for (var j = 0; j < 7; j++)
                        designers[j] ??= designer;
                    return new SimaiMetadata(title, 
                                             artist, 
                                             first, 
                                             designers,
                                             levels, 
                                             fumens, 
                                             commands.ToArray(), 
                                             await ComputeHashAsBase64StringAsync(Encoding.UTF8.GetBytes(content)));
                }
                catch (Exception e)
                {
                    throw new InvalidSimaiMarkupException(i + 1, "", "在maidata.txt第" + (i + 1) + "行:\n" + e.Message + "读取谱面时出现错误");
                }
            });
        }
        void ReadMetadata(ReadOnlySpan<char> simaiContent)
        {
            var title = "";
            var artist = "";
            var designer = "";
            var first = 0f;
            var designers = new string[7];
            var fumens = new string[7];
            var levels = new string[7];

            Span<int> controlIndexs = stackalloc int[0];
        }
        private string GetValue(string varline) => varline.Substring(varline.IndexOf('=') + 1).Trim();
        private static bool IsNote(char noteText)
        {
            Span<char> slideMarks = stackalloc char[15]
            {
                '1',
                '2',
                '3',
                '4',
                '5',
                '6',
                '7',
                '8',
                '9',
                '0',
                'A',
                'B',
                'C',
                'D',
                'E',
            }; ///ABCDE for touch
            foreach (var mark in slideMarks)
            {
                if (noteText == mark)
                    return true;
            }
            return false;
        }
        private static async Task<byte[]> ComputeHashAsync(byte[] data)
        {
            using var md5 = MD5.Create();
            return await Task.Run(() => md5.ComputeHash(data));
        }
        private static async Task<string> ComputeHashAsBase64StringAsync(byte[] data)
        {
            var hash = await ComputeHashAsync(data);

            return Convert.ToBase64String(hash);
        }
        public async Task<string> DeParseAsStringAsync(SimaiFile simaiFile)
        {
            return await Task.Run(() =>
            {
                var sb = new StringBuilder();
                var finalDesigner = string.Empty;

                sb.AppendLine($"&title={simaiFile.Title}");
                sb.AppendLine($"&artist={simaiFile.Artist}");
                sb.AppendLine($"&first={simaiFile.Offset}");
                for (int i = 0; i < 7; i++)
                {
                    var chart = simaiFile.Charts[i];
                    if (chart is null) continue;
                    if (!string.IsNullOrEmpty(chart.Designer))
                    {
                        finalDesigner = chart.Designer;
                        sb.AppendLine($"&des_{i + 1}={chart.Designer}");
                    }
                    if (!string.IsNullOrEmpty(chart.Level))
                    {
                        sb.AppendLine($"&lv_{i + 1}={chart.Level}");
                    }
                }
                sb.AppendLine($"&des={finalDesigner}");
                foreach (var command in simaiFile.Commands)
                {
                    sb.AppendLine($"&{command.Prefix}={command.Value}");
                }
                for (int i = 0; i < 7; i++)
                {
                    var chart = simaiFile.RawCharts[i];
                    if (string.IsNullOrEmpty(chart)) continue;
                    sb.AppendLine($"&inote_{i + 1}={chart}");
                }
                return sb.ToString();
            });
        }
        public string DeParseAsString(SimaiFile simaiFile) => DeParseAsStringAsync(simaiFile).Result;
        //Note: this method only deparse RawChart
        public void DeParse(SimaiFile simaiFile,string path)
        {
            var fumen = DeParseAsString(simaiFile);
            File.WriteAllText(path, fumen);
        }
        public async Task DeParseAsync(SimaiFile simaiFile, string path)
        {
            var fumen = await DeParseAsStringAsync(simaiFile);
            await File.WriteAllTextAsync(path, fumen);
        }
    }
}
