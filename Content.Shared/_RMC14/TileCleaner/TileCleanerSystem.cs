using Content.Shared.Decals;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Shared._RMC14.TileCleaner;

public sealed class TileCleanerSystem : EntitySystem
{
    [Dependency] private readonly SharedDecalSystem _decals = default!;
    [Dependency] private readonly EntityLookupSystem _entityLookupSystem = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<PlaceDecalActionEvent>(OnPlaceDecalAction);
        SubscribeLocalEvent<TileCleanerComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<TileCleanerComponent, ComponentRemove>(OnComponentRemove);
    }

    private void OnPlaceDecalAction(PlaceDecalActionEvent args)
    {
        //if (!TryGetComponent<MapGridComponent>(tile.GridUid, out var grid) ||
        //    !TryGetComponent<DecalGridComponent>(tile.GridUid, out var decalGrid))
        //{
        //    return;
        //}
        //var decals = _decals.GetDecalsIntersecting(tile.GridUid, _lookupSystem.GetLocalBounds(tile, grid.TileSize).Enlarged(0.5f).Translated(new Vector2(-0.5f, -0.5f)));
        //if (args.Target)
        //    _decals.GetDecalsIntersecting(mainGridUid, aabb);
    }

    private void OnInit(EntityUid uid, TileCleanerComponent comp, ComponentInit args)
    {
        //if(s)
    }

    private void OnComponentRemove(EntityUid uid, TileCleanerComponent comp, ComponentRemove args)
    {
        //if(s)
    }
}
