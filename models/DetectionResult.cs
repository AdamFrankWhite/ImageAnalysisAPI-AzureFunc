using Amazon.Rekognition.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImageAnalysisAPI.models
{
    public class DetectionResult
    {
        public DetectionResult() { }
        public List<Label> Labels { get; set; }
        public List<ModerationLabel> ExplicitContent { get; set; }
    }

}
