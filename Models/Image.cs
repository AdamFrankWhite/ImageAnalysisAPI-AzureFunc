using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImageAnalysisAPI.models { 

    public class Image
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonElement("OwnerId")]
        public string OwnerId { get; set; } = "owner";
        [BsonElement("Filename")]
        public string Filename { get; set; } = null!;
        [BsonElement("ImageUrl")]
        public string ImageUrl { get; set; } = null!;
        [BsonElement("ExplicitCategories")]
        public List<ExplicitCategory> ExplicitCategories { get; set; } = null!;
        [BsonElement("ExplicitParentCategories")]
        public List<ExplicitCategory> ExplicitParentCategories { get; set; } = null!;
    }
}
