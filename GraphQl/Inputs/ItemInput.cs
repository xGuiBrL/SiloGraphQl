namespace InventarioSilo.GraphQL.Inputs
{
    public class ItemInput
    {
        public string CategoriaId { get; set; } = null!;
        public string UbicacionId { get; set; } = null!;
        public string CodigoMaterial { get; set; } = null!;
        public string DescripcionMaterial { get; set; } = null!;
        public decimal CantidadStock { get; set; }
        public string UnidadMedida { get; set; } = null!;
    }
}
