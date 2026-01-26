namespace InventarioSilo.GraphQL.Inputs
{
    public class ActualizarUsuarioInput
    {
        public string Id { get; set; } = string.Empty;
        public string Usuario { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public string Rol { get; set; } = string.Empty;
        public string? Password { get; set; }
    }
}
