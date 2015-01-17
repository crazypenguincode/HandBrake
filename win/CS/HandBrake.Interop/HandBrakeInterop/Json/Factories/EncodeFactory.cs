﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="EncodeFactory.cs" company="HandBrake Project (http://handbrake.fr)">
//   This file is part of the HandBrake source code - It may be used under the terms of the GNU General Public License.
// </copyright>
// <summary>
//   The encode factory.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace HandBrake.Interop.Json.Factories
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Windows.Media.Animation;

    using HandBrake.Interop.HbLib;
    using HandBrake.Interop.Helpers;
    using HandBrake.Interop.Json.Anamorphic;
    using HandBrake.Interop.Json.Encode;
    using HandBrake.Interop.Model;
    using HandBrake.Interop.Model.Encoding;
    using HandBrake.Interop.Model.Scan;

    using Subtitle = HandBrake.Interop.Json.Encode.Subtitle;

    /// <summary>
    /// This factory takes the internal EncodeJob object and turns it into a set of JSON models
    /// that can be deserialized by libhb.
    /// </summary>
    internal class EncodeFactory
    {
        /*
         * TODO:
         * 1. OpenCL and HWD Support 
         * 2. Rotate Support
         */

        /// <summary>
        /// The create.
        /// </summary>
        /// <param name="job">
        /// The encode job.
        /// </param>
        /// <param name="title">
        /// The title.
        /// </param>
        /// <returns>
        /// The <see cref="JsonEncodeObject"/>.
        /// </returns>
        internal static JsonEncodeObject Create(EncodeJob job, Title title)
        {
            JsonEncodeObject encode = new JsonEncodeObject
                {
                    SequenceID = 0, 
                    Audio = CreateAudio(job), 
                    Destination = CreateDestination(job), 
                    Filter = CreateFilter(job, title), 
                    PAR = CreatePAR(job, title), 
                    MetaData = CreateMetaData(job), 
                    Source = CreateSource(job), 
                    Subtitle = CreateSubtitle(job), 
                    Video = CreateVideo(job)
                };

            return encode;
        }

        /// <summary>
        /// The create source.
        /// </summary>
        /// <param name="job">
        /// The job.
        /// </param>
        /// <returns>
        /// The <see cref="Source"/>.
        /// </returns>
        private static Source CreateSource(EncodeJob job)
        {
            Range range = new Range();
            switch (job.RangeType)
            {
                case VideoRangeType.Chapters:
                    range.ChapterEnd = job.ChapterEnd;
                    range.ChapterStart = job.ChapterStart;
                    break;
                case VideoRangeType.Seconds:
                    range.PtsToStart = (int)(job.SecondsStart * 90000);
                    range.PtsToStop = (int)((job.SecondsEnd - job.SecondsStart) * 90000); 
                    break;
                case VideoRangeType.Frames:
                    range.FrameToStart = job.FramesStart;
                    range.FrameToStop = job.FramesEnd; 
                    break;
                case VideoRangeType.Preview:
                    range.StartAtPreview = job.StartAtPreview;
                    range.SeekPoints = job.SeekPoints;
                    range.PtsToStop = job.SecondsEnd * 90000; 
                    break;
            }

            Source source = new Source
            {
                Title = job.Title, 
                Range = range,
                Angle = job.Angle
            };
            return source;
        }

        /// <summary>
        /// The create destination.
        /// </summary>
        /// <param name="job">
        /// The job.
        /// </param>
        /// <returns>
        /// The <see cref="Destination"/>.
        /// </returns>
        private static Destination CreateDestination(EncodeJob job)
        {
            Destination destination = new Destination
            {
                File = job.OutputPath, 
                Mp4Options = new Mp4Options
                                 {
                                     IpodAtom = job.EncodingProfile.IPod5GSupport, 
                                     Mp4Optimize = job.EncodingProfile.Optimize
                                 }, 
                ChapterMarkers = job.EncodingProfile.IncludeChapterMarkers, 
                Mux = HBFunctions.hb_container_get_from_name(job.EncodingProfile.ContainerName), 
                ChapterList = new List<ChapterList>()
            };

            if (job.UseDefaultChapterNames)
            {
                foreach (string item in job.CustomChapterNames)
                {
                    ChapterList chapter = new ChapterList { Name = item };
                    destination.ChapterList.Add(chapter);
                }
            }

            return destination;
        }

        /// <summary>
        /// Create the PAR object
        /// </summary>
        /// <param name="job">
        /// The Job
        /// </param>
        /// <param name="title">
        /// The title.
        /// </param>
        /// <returns>
        /// The produced PAR object.
        /// </returns>
        private static PAR CreatePAR(EncodeJob job, Title title)
        {
            Geometry resultGeometry = AnamorphicFactory.CreateGeometry(job, title, AnamorphicFactory.KeepSetting.HB_KEEP_WIDTH);
            return new PAR { Num = resultGeometry.PAR.Num, Den = resultGeometry.PAR.Den };
        }

        /// <summary>
        /// The create subtitle.
        /// </summary>
        /// <param name="job">
        /// The job.
        /// </param>
        /// <returns>
        /// The <see cref="Encode.Subtitle"/>.
        /// </returns>
        private static Subtitle CreateSubtitle(EncodeJob job)
        {
            Subtitle subtitle = new Subtitle
                {
                    Search =
                        new Search
                            {
                                Enable = false, 
                                Default = false, 
                                Burn = false, 
                                Forced = false
                            }, 
                    SubtitleList = new List<SubtitleList>()
                };

            foreach (SourceSubtitle item in job.Subtitles.SourceSubtitles)
            {
                // Handle Foreign Audio Search
                if (item.TrackNumber == 0)
                {
                    subtitle.Search.Enable = true;
                    subtitle.Search.Burn = item.BurnedIn;
                    subtitle.Search.Default = item.Default;
                    subtitle.Search.Forced = item.Forced;
                }
                else
                {
                    SubtitleList track = new SubtitleList { Burn = item.BurnedIn, Default = item.Default, Force = item.Forced, ID = item.TrackNumber, Track = item.TrackNumber };
                    subtitle.SubtitleList.Add(track);
                }
            }

            foreach (SrtSubtitle item in job.Subtitles.SrtSubtitles)
            {
                SubtitleList track = new SubtitleList
                    {
                        Track = -1, // Indicates SRT
                        Default = item.Default, 
                        Offset = item.Offset, 
                        Burn = item.BurnedIn,
                        SRT =
                            new SRT
                                {
                                    Filename = item.FileName, 
                                    Codeset = item.CharacterCode, 
                                    Language = item.LanguageCode
                                }
                    };

                subtitle.SubtitleList.Add(track);
            }

            return subtitle;
        }

        /// <summary>
        /// The create video.
        /// </summary>
        /// <param name="job">
        /// The job.
        /// </param>
        /// <returns>
        /// The <see cref="Video"/>.
        /// </returns>
        private static Video CreateVideo(EncodeJob job)
        {
            Video video = new Video();

            HBVideoEncoder videoEncoder = HandBrakeEncoderHelpers.VideoEncoders.FirstOrDefault(e => e.ShortName == job.EncodingProfile.VideoEncoder);
            Validate.NotNull(videoEncoder, "Video encoder " + job.EncodingProfile.VideoEncoder + " not recognized.");
            if (videoEncoder != null)
            {
                video.Codec = videoEncoder.Id;
            }

            video.TwoPass = job.EncodingProfile.TwoPass;
            video.Turbo = job.EncodingProfile.TurboFirstPass;
            video.Level = job.EncodingProfile.VideoLevel;
            video.Options = job.EncodingProfile.VideoOptions;
            video.Preset = job.EncodingProfile.VideoPreset;
            video.Profile = job.EncodingProfile.VideoProfile;
            video.Quality = job.EncodingProfile.Quality;
            if (job.EncodingProfile.VideoTunes != null && job.EncodingProfile.VideoTunes.Count > 0)
            {
                video.Tune = string.Join(",", job.EncodingProfile.VideoTunes);
            }

            return video;
        }

        /// <summary>
        /// The create audio.
        /// </summary>
        /// <param name="job">
        /// The job.
        /// </param>
        /// <returns>
        /// The <see cref="Audio"/>.
        /// </returns>
        private static Audio CreateAudio(EncodeJob job)
        {
            Audio audio = new Audio();

            if (!string.IsNullOrEmpty(job.EncodingProfile.AudioEncoderFallback))
            {
                HBAudioEncoder audioEncoder = HandBrakeEncoderHelpers.GetAudioEncoder(job.EncodingProfile.AudioEncoderFallback);
                Validate.NotNull(audioEncoder, "Unrecognized fallback audio encoder: " + job.EncodingProfile.AudioEncoderFallback);
                audio.FallbackEncoder = audioEncoder.Id;
            }

            audio.CopyMask = (int)NativeConstants.HB_ACODEC_ANY;

            audio.AudioList = new List<AudioList>();
            int numTracks = 0;
            foreach (AudioEncoding item in job.EncodingProfile.AudioEncodings)
            {
                HBAudioEncoder encoder = HandBrakeEncoderHelpers.GetAudioEncoder(item.Encoder);
                Validate.NotNull(encoder, "Unrecognized audio encoder:" + item.Encoder);

                HBMixdown mixdown = HandBrakeEncoderHelpers.GetMixdown(item.Mixdown);
                Validate.NotNull(mixdown, "Unrecognized audio mixdown:" + item.Mixdown);

                AudioList audioTrack = new AudioList
                    {
                        Track = numTracks++, 
                        DRC = item.Drc, 
                        Encoder = encoder.Id, 
                        Gain = item.Gain, 
                        Mixdown = mixdown.Id, 
                        NormalizeMixLevel = false, 
                        Samplerate = item.SampleRateRaw,
                        Name = item.Name,
                    };

                if (!item.IsPassthru)
                {
                    if (item.EncodeRateType == AudioEncodeRateType.Quality)
                    {
                        audioTrack.Quality = item.Quality;
                    }

                    if (item.EncodeRateType == AudioEncodeRateType.Compression)
                    {
                        audioTrack.CompressionLevel = item.Compression;
                    }

                    if (item.EncodeRateType == AudioEncodeRateType.Bitrate)
                    {
                        audioTrack.Bitrate = item.Bitrate;
                    }
                }

                audio.AudioList.Add(audioTrack);
            }

            return audio;
        }

        /// <summary>
        /// The create filter.
        /// </summary>
        /// <param name="job">
        /// The job.
        /// </param>
        /// <param name="title">
        /// The title.
        /// </param>
        /// <returns>
        /// The <see cref="Filter"/>.
        /// </returns>
        private static Filter CreateFilter(EncodeJob job, Title title)
        {
            Filter filter = new Filter
                            {
                                FilterList = new List<FilterList>(), 
                                Grayscale = job.EncodingProfile.Grayscale
                            };

            // Detelecine
            if (job.EncodingProfile.Detelecine != Detelecine.Off)
            {
                FilterList filterItem = new FilterList { ID = (int)hb_filter_ids.HB_FILTER_DETELECINE, Settings = job.EncodingProfile.CustomDetelecine };
                filter.FilterList.Add(filterItem);
            }

            // Decomb
            if (job.EncodingProfile.Decomb != Decomb.Off)
            {
                string options;
                if (job.EncodingProfile.Decomb == Decomb.Fast)
                {
                    options = "7:2:6:9:1:80";
                }
                else if (job.EncodingProfile.Decomb == Decomb.Bob)
                {
                    options = "455";
                }
                else
                {
                    options = job.EncodingProfile.CustomDecomb;
                }

                FilterList filterItem = new FilterList { ID = (int)hb_filter_ids.HB_FILTER_DECOMB, Settings = options };
                filter.FilterList.Add(filterItem);
            }

            // Deinterlace
            if (job.EncodingProfile.Deinterlace != Deinterlace.Off)
            {
                string options;
                if (job.EncodingProfile.Deinterlace == Deinterlace.Fast)
                {
                    options = "0";
                }
                else if (job.EncodingProfile.Deinterlace == Deinterlace.Slow)
                {
                    options = "1";
                }
                else if (job.EncodingProfile.Deinterlace == Deinterlace.Slower)
                {
                    options = "3";
                }
                else if (job.EncodingProfile.Deinterlace == Deinterlace.Bob)
                {
                    options = "15";
                }
                else
                {
                    options = job.EncodingProfile.CustomDeinterlace;
                }

                FilterList filterItem = new FilterList { ID = (int)hb_filter_ids.HB_FILTER_DEINTERLACE, Settings = options };
                filter.FilterList.Add(filterItem);
            }

            // VFR / CFR
            int fm = job.EncodingProfile.ConstantFramerate ? 1 : job.EncodingProfile.PeakFramerate ? 2 : 0;
            IntPtr frameratePrt = Marshal.StringToHGlobalAnsi(job.EncodingProfile.Framerate.ToString(CultureInfo.InvariantCulture));
            int vrate = HBFunctions.hb_video_framerate_get_from_name(frameratePrt);

            int num;
            int den;
            if (vrate > 0)
            {
                num = 27000000;
                den = vrate;
            }
            else
            {
                // cfr or pfr flag with no rate specified implies use the title rate.
                num = title.FramerateNumerator;
                den = title.FramerateDenominator;
            }

            string framerateString = string.Format("{0}:{1}:{2}", fm, num, den); // filter_cfr, filter_vrate.num, filter_vrate.den

            FilterList framerateShaper = new FilterList { ID = (int)hb_filter_ids.HB_FILTER_VFR, Settings = framerateString };
            filter.FilterList.Add(framerateShaper);

            // Deblock
            if (job.EncodingProfile.Deblock >= 5)
            {
                FilterList filterItem = new FilterList { ID = (int)hb_filter_ids.HB_FILTER_DEBLOCK, Settings = job.EncodingProfile.Deblock.ToString() };
                filter.FilterList.Add(filterItem);
            }

            // Denoise
            if (job.EncodingProfile.Denoise != Denoise.Off)
            {
                hb_filter_ids id = job.EncodingProfile.Denoise == Denoise.hqdn3d
                    ? hb_filter_ids.HB_FILTER_HQDN3D
                    : hb_filter_ids.HB_FILTER_NLMEANS;

                string settings;
                if (job.EncodingProfile.Denoise == Denoise.hqdn3d
                    && !string.IsNullOrEmpty(job.EncodingProfile.CustomDenoise))
                {
                    settings = job.EncodingProfile.CustomDenoise;
                }
                else
                {
                    IntPtr settingsPtr = HBFunctions.hb_generate_filter_settings((int)id, job.EncodingProfile.DenoisePreset, job.EncodingProfile.DenoiseTune);
                    settings = Marshal.PtrToStringAnsi(settingsPtr);
                }

                FilterList filterItem = new FilterList { ID = (int)id, Settings = settings };
                filter.FilterList.Add(filterItem);
            }

            // CropScale Filter
            Geometry resultGeometry = AnamorphicFactory.CreateGeometry(job, title, AnamorphicFactory.KeepSetting.HB_KEEP_WIDTH);
            FilterList cropScale = new FilterList
            {
                ID = (int)hb_filter_ids.HB_FILTER_CROP_SCALE, 
                Settings =
                    string.Format(
                        "{0}:{1}:{2}:{3}:{4}:{5}", 
                        resultGeometry.Width, 
                        resultGeometry.Height, 
                        job.EncodingProfile.Cropping.Top, 
                        job.EncodingProfile.Cropping.Bottom, 
                        job.EncodingProfile.Cropping.Left, 
                        job.EncodingProfile.Cropping.Right)
            };
            filter.FilterList.Add(cropScale);

            // Rotate
            /* TODO  NOT SUPPORTED YET. */

            return filter;
        }

        /// <summary>
        /// The create meta data.
        /// </summary>
        /// <param name="job">
        /// The job.
        /// </param>
        /// <returns>
        /// The <see cref="MetaData"/>.
        /// </returns>
        private static MetaData CreateMetaData(EncodeJob job)
        {
            MetaData metaData = new MetaData();

            /* TODO  NOT SUPPORTED YET. */
            return metaData;
        }
    }
}