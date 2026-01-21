using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace InventarioSilo.Models
{
    public class Entrega
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonRepresentation(BsonType.ObjectId)]
        public string ItemId { get; set; } = null!;
        public DateTime Fecha { get; set; }
        public string EntregadoA { get; set; } = null!;
        public string CodigoMaterial { get; set; } = null!;
        public string DescripcionMaterial { get; set; } = null!;
        public decimal CantidadEntregada { get; set; }
        public string UnidadMedida { get; set; } = null!;
        public string? Observaciones { get; set; }
        public bool EsSinRegistro { get; set; }
    }
}
