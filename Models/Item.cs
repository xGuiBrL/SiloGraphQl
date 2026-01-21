using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace InventarioSilo.Models
{
    public class Item
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        public string CodigoMaterial { get; set; } = null!;
        public string NombreMaterial { get; set; } = null!;
        public string DescripcionMaterial { get; set; } = null!;
        public decimal CantidadStock { get; set; }
        public string Localizacion { get; set; } = null!;
        public string UnidadMedida { get; set; } = null!;
    }
}
