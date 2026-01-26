using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace InventarioSilo.Models
{
    public class Categoria
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        public string Nombre { get; set; } = null!;
        public string Descripcion { get; set; } = null!;
    }
}
