using Content.Shared.Decals;

namespace Content.Shared._RMC14.TileCleaner;

public sealed class TileCleanerSystem : EntitySystem
{
    public override void Initialize()
    {
        SubscribeLocalEvent<PlaceDecalActionEvent>(OnPlaceDecalAction);
    }

    private void OnPlaceDecalAction(PlaceDecalActionEvent args)
    {
        //if(s)
    }
}
