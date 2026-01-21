namespace InventarioSilo.GraphQL.Inputs
{
    public class RecepcionInput
    {
        public string RecibidoDe { get; set; } = null!;
        public string CodigoMaterial { get; set; } = null!;
        public string DescripcionMaterial { get; set; } = null!;
        public decimal CantidadRecibida { get; set; }
        public string UnidadMedida { get; set; } = null!;
        public string? Observaciones { get; set; }
    }
}
