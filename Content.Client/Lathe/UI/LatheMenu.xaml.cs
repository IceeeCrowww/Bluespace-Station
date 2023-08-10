﻿using System.Linq;
using System.Text;
using Content.Client.Stylesheets;
using Content.Shared.Lathe;
using Content.Shared.Materials;
using Content.Shared.Research.Prototypes;
using Robust.Client.AutoGenerated;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.Prototypes;

namespace Content.Client.Lathe.UI;

[GenerateTypedNameReferences]
public sealed partial class LatheMenu : DefaultWindow
{
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    private readonly SpriteSystem _spriteSystem;
    private readonly LatheSystem _lathe;

    public event Action<BaseButton.ButtonEventArgs>? OnQueueButtonPressed;
    public event Action<BaseButton.ButtonEventArgs>? OnServerListButtonPressed;
    public event Action<string, int>? RecipeQueueAction;

    public List<string> Recipes = new();

    public LatheMenu(LatheBoundUserInterface owner)
    {
        RobustXamlLoader.Load(this);
        IoCManager.InjectDependencies(this);

        _spriteSystem = _entityManager.System<SpriteSystem>();
        _lathe = _entityManager.System<LatheSystem>();

        Title = _entityManager.GetComponent<MetaDataComponent>(owner.Owner).EntityName;

        SearchBar.OnTextChanged += _ =>
        {
            PopulateRecipes(owner.Owner);
        };
        AmountLineEdit.OnTextChanged += _ =>
        {
            PopulateRecipes(owner.Owner);
        };

        QueueButton.OnPressed += a => OnQueueButtonPressed?.Invoke(a);
        ServerListButton.OnPressed += a => OnServerListButtonPressed?.Invoke(a);

        if (_entityManager.TryGetComponent<LatheComponent>(owner.Owner, out var latheComponent))
        {
            if (!latheComponent.DynamicRecipes.Any())
            {
                ServerListButton.Visible = false;
                QueueButton.RemoveStyleClass(StyleBase.ButtonOpenRight);
                //QueueButton.AddStyleClass(StyleBase.ButtonSquare);
            }
        }
    }

    public void PopulateMaterials(EntityUid lathe)
    {
        if (!_entityManager.TryGetComponent<MaterialStorageComponent>(lathe, out var materials))
            return;

        Materials.Clear();

        foreach (var (id, amount) in materials.Storage)
        {
            if (!_prototypeManager.TryIndex(id, out MaterialPrototype? material))
                continue;
            var name = Loc.GetString(material.Name);
            var mat = Loc.GetString("lathe-menu-material-display",
                ("material", name), ("amount", amount));
            Materials.AddItem(mat, _spriteSystem.Frame0(material.Icon), false);
        }

        if (Materials.Count == 0)
        {
            var noMaterialsMsg = Loc.GetString("lathe-menu-no-materials-message");
            Materials.AddItem(noMaterialsMsg, null, false);
        }

        PopulateRecipes(lathe);
    }

    /// <summary>
    /// Populates the list of all the recipes
    /// </summary>
    /// <param name="lathe"></param>
    public void PopulateRecipes(EntityUid lathe)
    {
        if (!_entityManager.TryGetComponent<LatheComponent>(lathe, out var component))
            return;

        var recipesToShow = new List<LatheRecipePrototype>();
        foreach (var recipe in Recipes)
        {
            if (!_prototypeManager.TryIndex<LatheRecipePrototype>(recipe, out var proto))
                continue;

            if (SearchBar.Text.Trim().Length != 0)
            {
                if (proto.Name.ToLowerInvariant().Contains(SearchBar.Text.Trim().ToLowerInvariant()))
                    recipesToShow.Add(proto);
            }
            else
            {
                recipesToShow.Add(proto);
            }
        }

        if (!int.TryParse(AmountLineEdit.Text, out var quantity) || quantity <= 0)
            quantity = 1;

        RecipeList.Children.Clear();
        foreach (var prototype in recipesToShow)
        {
            StringBuilder sb = new();
            var first = true;
            foreach (var (id, amount) in prototype.RequiredMaterials)
            {
                if (!_prototypeManager.TryIndex<MaterialPrototype>(id, out var proto))
                    continue;

                if (first)
                    first = false;
                else
                    sb.Append('\n');

                var adjustedAmount = SharedLatheSystem.AdjustMaterial(amount, prototype.ApplyMaterialDiscount, component.MaterialUseMultiplier);

                sb.Append(adjustedAmount);
                sb.Append(' ');
                sb.Append(Loc.GetString(proto.Name));
            }

            var icon = prototype.Icon == null
                ? _spriteSystem.GetPrototypeIcon(prototype.Result).Default
                : _spriteSystem.Frame0(prototype.Icon);
            var canProduce = _lathe.CanProduce(lathe, prototype, quantity);

            var control = new RecipeControl(prototype, sb.ToString(), canProduce, icon);
            control.OnButtonPressed += s =>
            {
                if (!int.TryParse(AmountLineEdit.Text, out var amount) || amount <= 0)
                    amount = 1;
                RecipeQueueAction?.Invoke(s, amount);
            };
            RecipeList.AddChild(control);
        }
    }
}
