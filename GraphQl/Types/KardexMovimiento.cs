namespace InventarioSilo.GraphQL.Types
{
    public class KardexMovimiento
    {
        public DateTime Fecha { get; set; }
        public string Tipo { get; set; } = null!; // ENTRADA / SALIDA
        public string Referencia { get; set; } = null!;
        public string Descripcion { get; set; } = null!;
        public string? Observaciones { get; set; }
        public decimal Cantidad { get; set; }
        public string UnidadMedida { get; set; } = null!;
        public string Origen { get; set; } = null!; // RECEPCION / ENTREGA
        public string? RegistroId { get; set; }
        public bool EsSinRegistro { get; set; }
    }
}
