
public interface ShopData
{ 
    public string Id { get; }
    public string Name { get; }
    public SalesLocationType SalesLocationType { get; }

    public MoneyType MoneyType { get; }

    public int BuyScore { get; }

    public int BuyPrice { get; }
}
