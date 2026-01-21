namespace InventarioSilo.GraphQL.Inputs
{
    public class ItemInput
    {
        public string CodigoMaterial { get; set; } = null!;
        public string NombreMaterial { get; set; } = null!;
        public string DescripcionMaterial { get; set; } = null!;
        public decimal CantidadStock { get; set; }
        public string Localizacion { get; set; } = null!;
        public string UnidadMedida { get; set; } = null!;
    }
}
