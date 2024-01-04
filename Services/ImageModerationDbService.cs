using ImageAnalysisAPI.models;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using MongoDB.Driver.Core.Configuration;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ImageAnalysisAPI.Services
{
    internal class ImageModerationDbService
    {
        
        private readonly IMongoCollection<Image> _imagesCollection;

        public ImageModerationDbService(
            string ConnectionString, string DatabaseName, string ImagesCollectionName)
        {
            var mongoClient = new MongoClient(
                ConnectionString);

            var mongoDatabase = mongoClient.GetDatabase(
                 DatabaseName);

            _imagesCollection = mongoDatabase.GetCollection<Image>(
                 ImagesCollectionName);
        }

        public async Task<List<Image>> GetAsync() =>
            await _imagesCollection.Find(_ => true).ToListAsync();

        public async Task<Image?> GetAsync(string id) =>
            await _imagesCollection.Find(x => x.Id == id).FirstOrDefaultAsync();

        public async Task CreateAsync(Image newImage) =>
            await _imagesCollection.InsertOneAsync(newImage);

        public async Task UpdateAsync(string id, Image updatedImage) =>
            await _imagesCollection.ReplaceOneAsync(x => x.Id == id, updatedImage);

        public async Task RemoveAsync(string id) =>
            await _imagesCollection.DeleteOneAsync(x => x.Id == id);
    }
}

