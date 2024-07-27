using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace FFmpegUnityBind2
{
    using static Instructions;

    class ImagesToVideoWithWatermark : BaseCommand
    {
        readonly float fps;
        readonly int crf;
        readonly string watermarkPath;
        readonly Vector2 position;
        readonly float scale;
        readonly float speed;

        public ImagesToVideoWithWatermark(string framePathFormat, string watermarkPath, string outputPath, float fps, int crf, Vector2 position, float scale, float speed)
            : base(framePathFormat, outputPath)
        {
            this.fps = fps;
            this.crf = crf;
            this.watermarkPath = TryEnclosePath(watermarkPath);
            this.position = position;
            this.scale = scale;
            this.speed = speed;
        }

        /// <summary>
        /// Example:
        /// -y -framerate 25 -f image2 -i .../frame_%04d.png -i watermark.png -filter_complex "[1:v]scale=iw*0.2:ih*0.2[wm];[0:v][wm]overlay=W-w-10:H-h-10,setpts=PTS/2" -vcodec libx264 -crf 23 -pix_fmt yuv420p .../output.mp4
        /// </summary>
        public override string ToString()
        {
            var stringBuilder = new StringBuilder();

            // Input Image sequence params
            stringBuilder
                .Append(REWRITE_INSTRUCTION)
                .Append(SPACE)
                .Append(FRAMERATE_INSTRUCTION)
                .Append(SPACE)
                .Append(fps)
                .Append(SPACE)
                .Append(FORCE_INPUT_OR_OUTPUT_INSTRUCTION)
                .Append(SPACE)
                .Append(IMAGE_FORMAT_INSTRUCTION)
                .Append(SPACE)
                .Append(INPUT_INSTRUCTION)
                .Append(SPACE)
                .Append(InputPaths.First());

            // Input Watermark params
            stringBuilder
                .Append(SPACE)
                .Append(INPUT_INSTRUCTION)
                .Append(SPACE)
                .Append(watermarkPath);

            // Overlay Filter params with scaling and speed adjustment
            stringBuilder
                .Append(SPACE)
                .Append(FILTER_COMPLEX_INSTRUCTION)
                .Append(SPACE)
                .Append($"\"[1:v]scale=iw*{scale}:ih*{scale}[wm];[0:v][wm]overlay=W-w-{position.x * 100}:H-h-{position.y * 100},setpts=PTS/{speed}\"");

            // Output Video params
            stringBuilder
                .Append(SPACE)
                .Append(VIDEO_CODEC_INSTRUCTION)
                .Append(SPACE)
                .Append(LIB_X264_INSTRUCTION)
                .Append(SPACE)
                .Append(CONSTANT_RATE_FACTOR_INSTRUCTION)
                .Append(SPACE)
                .Append(crf)
                .Append(SPACE)
                .Append(PIXEL_FORMAT_INSTRUCTION)
                .Append(SPACE)
                .Append(YUV_420P_INSTRUCTION)
                .Append(SPACE)
                .Append(OutputPath);

            return stringBuilder.ToString();
        }
    }
}
