using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImageAnalysisAPI.models
{
    internal class ImageModerationDatabaseSettings
    {
       
        public string ConnectionString { get; set; } = null!;

        public string DatabaseName { get; set; } = null!;

        public string ImagesCollectionName { get; set; } = null!;
    }

}

