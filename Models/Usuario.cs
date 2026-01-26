using InventarioSilo.Security;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace InventarioSilo.Models
{
    public class Usuario
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonElement("usuario")]
        public string NombreUsuario { get; set; } = string.Empty;

        [BsonElement("password")]
        public string Password { get; set; } = string.Empty;

        [BsonElement("nombre")]
        public string Nombre { get; set; } = string.Empty;

        [BsonElement("rol")]
        public string Rol { get; set; } = UserRoles.Usuario;
    }
}
