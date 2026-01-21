namespace InventarioSilo.GraphQL.Inputs
{
    public class RegistrarUsuarioInput
    {
        public string Usuario { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
    }
}
