namespace InventarioSilo.GraphQL.Types
{
    public class ReporteMensual
    {
        public string? ItemId { get; set; }
        public string CodigoMaterial { get; set; } = null!;
        public string NombreMaterial { get; set; } = null!;
        public string DescripcionMaterial { get; set; } = null!;
        public string Localizacion { get; set; } = null!;
        public decimal TotalEntradas { get; set; }
        public decimal TotalSalidas { get; set; }
        public string UnidadMedida { get; set; } = null!;
        public decimal TotalEntradasSinRegistro { get; set; }
        public decimal TotalSalidasSinRegistro { get; set; }
        public decimal StockDespuesBalance { get; set; }
    }
}
