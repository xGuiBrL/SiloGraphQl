namespace InventarioSilo.GraphQL.Types
{
    public class KardexItem
    {
        public string CodigoMaterial { get; set; } = null!;
        public string NombreMaterial { get; set; } = null!;
        public decimal StockActual { get; set; }
        public IEnumerable<KardexMovimiento> Movimientos { get; set; } = new List<KardexMovimiento>();
    }
}
