namespace InventarioSilo.GraphQL.Inputs
{
    public class ItemUpdateInput : ItemInput
    {
        public string Id { get; set; } = null!;
    }
}
