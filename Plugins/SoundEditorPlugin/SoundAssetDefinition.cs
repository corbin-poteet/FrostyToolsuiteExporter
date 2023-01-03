using Frosty.Core;
using Frosty.Core.Controls;
using Frosty.Core.Windows;
using FrostySdk;
using FrostySdk.Interfaces;
using FrostySdk.IO;
using FrostySdk.Managers.Entries;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WaveFormRendererLib;

namespace SoundEditorPlugin
{
    public class SoundWaveAssetOverride : BaseTypeOverride
    {
//#if FROSTY_DEVELOPER
        public BaseFieldOverride Chunks { get; set; }
        public BaseFieldOverride RuntimeVariations { get; set; }
        public BaseFieldOverride Segments { get; set; }
        public BaseFieldOverride Localization { get; set; }
        public BaseFieldOverride SubtitleStringIds { get; set; }
        public BaseFieldOverride Subtitles { get; set; }
//#else
        //[IsHidden]
        //public BaseFieldOverride Chunks { get; set; }
        //[IsHidden]
        //public BaseFieldOverride RuntimeVariations { get; set; }
        //[IsHidden]
        //public BaseFieldOverride Segments { get; set; }
        //[IsHidden]
        //public BaseFieldOverride Localization { get; set; }
        //[IsHidden]
        //public BaseFieldOverride SubtitleStringIds { get; set; }
        //[IsHidden]
        //public BaseFieldOverride Subtitles { get; set; }
//#endif
    }

    public class SoundAssetDefinition : AssetDefinition
    {
        protected static ImageSource imageSource = new ImageSourceConverter().ConvertFromString("pack://application:,,,/SoundEditorPlugin;component/Images/SoundFileType.png") as ImageSource;
        public override ImageSource GetIcon()
        {
            return imageSource;
        }

        public override List<EbxAssetEntry> BatchExport(List<EbxAssetEntry> entries, string path, Stopwatch stopWatch)
        {
            FrostyTaskWindow.Show("Exporting Audio", "", task =>
            {
                for (int t = entries.Count - 1; t >= 0; t--)
                {
                    if (entries[t].Type != "SoundWaveAsset")
                    {
                        continue;
                    }

                    string assetName = Path.GetFileName(entries[t].Name);

                    // get ebx
                    EbxAsset asset = App.AssetManager.GetEbx(entries[t]);
                    dynamic soundWave = (dynamic)asset.RootObject;

                    // get runtime variations
                    int index = 0;
                    int totalCount = soundWave.RuntimeVariations.Count;
                    List<SoundDataTrack> tracks = new List<SoundDataTrack>();
                    for (int j = 0; j < soundWave.RuntimeVariations.Count; j++)
                    {
                        dynamic runtimeVariation = soundWave.RuntimeVariations[j];

                        string var = soundWave.RuntimeVariations.Count > 1 ? "_" + (j + 1).ToString("D" + (Math.Max(Math.Floor(Math.Log10(soundWave.RuntimeVariations.Count) + 1), 2))) : "";
                        task.Update(status: "Loading " + assetName + var, progress: ((index + 1) / (double)totalCount) * 100.0d);

                        SoundDataTrack track = new SoundDataTrack { Name = "Track #" + ((index++) + 1) };

                        dynamic soundDataChunk = soundWave.Chunks[runtimeVariation.ChunkIndex];
                        ChunkAssetEntry chunkEntry = App.AssetManager.GetChunkEntry(soundDataChunk.ChunkId);
                        if (chunkEntry == null)
                        {
                            continue;
                        }

                        using (NativeReader reader = new NativeReader(App.AssetManager.GetChunk(chunkEntry)))
                        {
                            List<short> decodedSoundBuf = new List<short>();
                            double startLoopingTime = 0.0;
                            double loopingDuration = 0.0;

                            for (int i = 0; i < runtimeVariation.SegmentCount; i++)
                            {
                                var segment = soundWave.Segments[runtimeVariation.FirstSegmentIndex + i];
                                reader.Position = segment.SamplesOffset;

                                if (reader.ReadUShort() != 0x48)
                                {
                                    //logger.LogError("Wrong Sample Offset at Variation {0}, Segment {1}", index, i);
                                    //return retVal;
                                    break;
                                }

                                ushort headersize = reader.ReadUShort(Endian.Big);
                                byte codec = (byte)(reader.ReadByte() & 0xF);
                                int channels = (reader.ReadByte() >> 2) + 1;
                                ushort sampleRate = reader.ReadUShort(Endian.Big);
                                uint sampleCount = reader.ReadUInt(Endian.Big) & 0xFFFFFFF;
                                //reader.Position += headersize - 0x0C;
                                switch (codec)
                                {
                                    case 0x1: track.Codec = "Unknown"; break;
                                    case 0x2: track.Codec = "PCM 16 Big"; break;
                                    case 0x3: track.Codec = "EA-XMA"; break;
                                    case 0x4: track.Codec = "XAS Interleaved v1"; break;
                                    case 0x5: track.Codec = "EALayer3 Interleaved v1"; break;
                                    case 0x6: track.Codec = "EALayer3 Interleaved v2 PCM"; break;
                                    case 0x7: track.Codec = "EALayer3 Interleaved v2 Spike"; break;
                                    case 0x9: track.Codec = "EASpeex"; break;
                                    case 0xA: track.Codec = "Unknown"; break;
                                    case 0xB: track.Codec = "EA-MP3"; break;
                                    case 0xC: track.Codec = "EAOpus"; break;
                                    case 0xD: track.Codec = "EAAtrac9"; break;
                                    case 0xE: track.Codec = "MultiStream Opus"; break;
                                    case 0xF: track.Codec = "MultiStream Opus (Uncoupled)"; break;
                                }

                                if (i == runtimeVariation.FirstLoopSegmentIndex && runtimeVariation.SegmentCount > 1)
                                {
                                    startLoopingTime = (decodedSoundBuf.Count / channels) / (double)sampleRate;
                                    track.LoopStart = (uint)decodedSoundBuf.Count;
                                }

                                reader.Position = segment.SamplesOffset;
                                byte[] soundBuf = reader.ReadToEnd();

                                if (codec == 0x2)
                                {
                                    short[] data = Pcm16b.Decode(soundBuf);
                                    decodedSoundBuf.AddRange(data);
                                    sampleCount = (uint)data.Length;
                                }
                                else if (codec == 0x4)
                                {
                                    short[] data = XAS.Decode(soundBuf);
                                    decodedSoundBuf.AddRange(data);
                                    sampleCount = (uint)data.Length;
                                }
                                else if (codec == 0x5 || codec == 0x6)
                                {
                                    sampleCount = 0;
                                    EALayer3.Decode(soundBuf, soundBuf.Length, (short[] data, int count, EALayer3.StreamInfo info) =>
                                    {
                                        if (info.streamIndex == -1)
                                            return;
                                        sampleCount += (uint)data.Length;
                                        decodedSoundBuf.AddRange(data);
                                    });
                                }

                                if (i == runtimeVariation.LastLoopSegmentIndex && runtimeVariation.SegmentCount > 1)
                                {
                                    loopingDuration = ((decodedSoundBuf.Count / channels) / (double)sampleRate) - startLoopingTime;
                                    track.LoopEnd = (uint)decodedSoundBuf.Count;
                                }

                                track.SampleRate = sampleRate;
                                track.ChannelCount = channels;
                                if (segment.SegmentLength == 0)
                                    segment.SegmentLength = (decodedSoundBuf.Count / track.ChannelCount) / (float)sampleRate;
                            }
                            track.Duration = (decodedSoundBuf.Count / track.ChannelCount) / (double)track.SampleRate;
                            track.Samples = decodedSoundBuf.ToArray();
                            track.SegmentCount = runtimeVariation.SegmentCount;
                        }
                        tracks.Add(track);
                    }

                    // export each runtime variation
                    for (int j = 0; j < tracks.Count; j++)
                    {
                        SoundDataTrack track = tracks[j];
                        WAV.WAVFormatChunk fmt = new WAV.WAVFormatChunk(WAV.WAVFormatChunk.DataFormats.WAVE_FORMAT_PCM, (ushort)track.ChannelCount, (uint)track.SampleRate, (uint)(track.ChannelCount * 2 * track.SampleRate), (ushort)(2 * track.ChannelCount), 16);
                        List<WAV.WAVDataFrame> frames = new List<WAV.WAVDataFrame>();

                        for (int i = 0; i < track.Samples.Length / track.ChannelCount; i++)
                        {
                            // write frame
                            WAV.WAV16BitDataFrame frame = new WAV.WAV16BitDataFrame((ushort)track.ChannelCount);
                            for (int channel = 0; channel < track.ChannelCount; channel++)
                            {
                                frame.Data[channel] = track.Samples[i * track.ChannelCount + channel];
                            }
                            frames.Add(frame);
                        }

                        WAV.WAVDataChunk data = new WAV.WAVDataChunk(fmt, frames);
                        WAV.RIFFMainChunk main = new WAV.RIFFMainChunk(new WAV.RIFFChunkHeader(0, new byte[] { 0x52, 0x49, 0x46, 0x46 }, 0), new byte[] { 0x57, 0x41, 0x56, 0x45 });

                        // define combine user defined path with the asset's data explorer path
                        string assetPath = Path.Combine(path, entries[t].Path);
                        string var = tracks.Count > 1 ? "_" + (j + 1).ToString("D" + (Math.Max(Math.Floor(Math.Log10(tracks.Count) + 1), 2))) : "";
                        string assetFullPath = Path.Combine(path, entries[t].Name + var + ".wav");
                        System.IO.Directory.CreateDirectory(assetPath);
                        task.Update(status: "Writing " + assetName + "_" + var, progress: ((index + 1) / (double)totalCount) * 100.0d);
                        
                        using (FileStream stream = new FileStream(assetFullPath, FileMode.Create))
                        using (BinaryWriter writer = new BinaryWriter(stream))
                        {
                            main.Write(writer, new List<WAV.IRIFFChunk>(new WAV.IRIFFChunk[] { fmt, data }));
                        }
                    }

                    entries.RemoveAt(t);

                }
            });
            
            
            
            return entries;
        }

    }

    public class SoundWaveAssetDefinition : SoundAssetDefinition
    {
        public override FrostyAssetEditor GetEditor(ILogger logger)
        {
            return new FrostySoundWaveEditor(logger);
        }
    }
    public class NewWaveAssetDefinition : SoundAssetDefinition
    {
        public override FrostyAssetEditor GetEditor(ILogger logger)
        {
            return new FrostyNewWaveEditor(logger);
        }
    }
    public class HarmonySampleBankAssetDefinition : SoundAssetDefinition
    {
        public override FrostyAssetEditor GetEditor(ILogger logger)
        {
            return new FrostyHarmonySampleBankEditor(logger);
        }
    }
    public class OctaneAssetDefinition : SoundAssetDefinition
    {
        public override FrostyAssetEditor GetEditor(ILogger logger)
        {
            return new FrostyOctaneSoundEditor(logger);
        }
    }
    public class ImpulseResponseAssetDefinition : SoundAssetDefinition
    {
        public override FrostyAssetEditor GetEditor(ILogger logger)
        {
            return new FrostyImpulseResponseEditor(logger);
        }
    }
}
