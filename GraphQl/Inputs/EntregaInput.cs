namespace InventarioSilo.GraphQL.Inputs
{
    public class EntregaInput
    {
        public string EntregadoA { get; set; } = null!;
        public string CodigoMaterial { get; set; } = null!;
        public string? ItemId { get; set; }
        public string DescripcionMaterial { get; set; } = null!;
        public decimal CantidadEntregada { get; set; }
        public string UnidadMedida { get; set; } = null!;
        public string? Observaciones { get; set; }
    }
}
