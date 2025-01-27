public struct CustomerVisitState
{
    private bool _isScoreValid;
    public bool IsScoreValid => _isScoreValid;

    private bool _isGiveRecipe;
    public bool IsGiveRecipe => _isGiveRecipe;

    private bool _isGiveItem;
    public bool IsGiveItem => _isGiveItem;

    private bool _isNotDefalut;
    public bool IsNotDefalut => _isNotDefalut;

    public CustomerVisitState(bool isScoreValid, bool isGiveRecipe, bool isGiveItem)
    {
        _isScoreValid = isScoreValid;
        _isGiveRecipe = isGiveRecipe;
        _isGiveItem = isGiveItem;
        _isNotDefalut = true;
    }

}
