using MongoDB.Driver;
using InventarioSilo.Settings;
using InventarioSilo.Models;

namespace InventarioSilo.Data
{
    public class MongoDbContext
    {
        private readonly IMongoDatabase _database;

        public MongoDbContext(MongoDbSettings settings)
        {
            var client = new MongoClient(settings.ConnectionString);
            _database = client.GetDatabase(settings.DatabaseName);
        }

        public IMongoCollection<T> GetCollection<T>(string name)
            => _database.GetCollection<T>(name);

        public IMongoCollection<Usuario> Usuarios => _database.GetCollection<Usuario>("Usuarios");
        public IMongoCollection<Categoria> Categorias => _database.GetCollection<Categoria>("Categorias");
        public IMongoCollection<Ubicacion> Ubicaciones => _database.GetCollection<Ubicacion>("Ubicaciones");
    }
}
