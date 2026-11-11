using Maf.Agent.Api.Models.Domain;

namespace Maf.Agent.Api.Models.Application;

public sealed class ModelDefaultRegistry(
    IModelProviderStore providers,
    IModelDefaultStore defaults)
{
    public async Task<IReadOnlyList<ModelDefaultSelection>> ListAsync(
        CancellationToken cancellationToken)
    {
        IReadOnlyList<ModelProvider> knownProviders = await providers.ListAsync(cancellationToken);
        IReadOnlyList<ModelDefault> stored = await defaults.ListAsync(cancellationToken);
        bool changed = false;
        List<ModelDefaultSelection> rows = [];
        foreach (ModelOperation operation in Enum.GetValues<ModelOperation>())
        {
            IReadOnlyList<ModelDefaultChoice> choices = Choices(knownProviders, operation);
            ModelDefault? current = stored.SingleOrDefault(item => item.Operation == operation);
            ModelDefaultChoice? selected = null;
            if (current is not null)
            {
                selected = Match(knownProviders, current);
                if (selected is null)
                {
                    defaults.Remove(current);
                    changed = true;
                }
            }

            rows.Add(new ModelDefaultSelection(operation, selected, choices));
        }

        if (changed)
        {
            await defaults.SaveChangesAsync(cancellationToken);
        }

        return rows;
    }

    public async Task<IReadOnlyList<ModelDefaultSelection>> SaveAsync(
        IReadOnlyList<SaveModelDefault> commands,
        CancellationToken cancellationToken)
    {
        ModelOperation[] operations = Enum.GetValues<ModelOperation>();
        if (commands.Count != operations.Length ||
            commands.Select(command => command.Operation).Distinct().Count() != operations.Length)
        {
            throw new ArgumentException("برای هر عملیات باید دقیقاً یک مدل پیش‌فرض مشخص شود.");
        }

        IReadOnlyList<ModelProvider> knownProviders = await providers.ListAsync(cancellationToken);
        IReadOnlyList<ModelDefault> stored = await defaults.ListAsync(cancellationToken);
        foreach (SaveModelDefault command in commands)
        {
            ModelDefault? current = stored.SingleOrDefault(item => item.Operation == command.Operation);
            if (command.ProviderId is null && string.IsNullOrWhiteSpace(command.ModelName))
            {
                if (current is not null)
                {
                    defaults.Remove(current);
                }

                continue;
            }

            if (command.ProviderId is null || string.IsNullOrWhiteSpace(command.ModelName))
            {
                throw new ArgumentException("ارائه‌دهنده و مدل باید با هم انتخاب شوند.");
            }

            ModelProvider provider = knownProviders.SingleOrDefault(item => item.Id == command.ProviderId)
                ?? throw new ArgumentException("ارائه‌دهنده مدل پیدا نشد.");
            string modelName = command.ModelName.Trim();
            ProviderModel model = provider.Models.FirstOrDefault(item => item.Name == modelName)
                ?? throw new ArgumentException($"مدل «{modelName}» در این ارائه‌دهنده نیست.");
            current ??= ModelDefault.Create(command.Operation);
            current.Assign(provider.Id, model.Name, model.Capabilities);
            if (!stored.Contains(current))
            {
                defaults.Add(current);
            }
        }

        await defaults.SaveChangesAsync(cancellationToken);
        return await ListAsync(cancellationToken);
    }

    private static ModelDefaultChoice? Match(
        IReadOnlyList<ModelProvider> knownProviders,
        ModelDefault current)
    {
        ModelProvider? provider = knownProviders.SingleOrDefault(item => item.Id == current.ProviderId);
        ProviderModel? model = provider?.Models.FirstOrDefault(item => item.Name == current.ModelName);
        return provider is not null && current.Accepts(model)
            ? new ModelDefaultChoice(provider.Id, provider.Name, model!.Name)
            : null;
    }

    private static IReadOnlyList<ModelDefaultChoice> Choices(
        IReadOnlyList<ModelProvider> knownProviders,
        ModelOperation operation)
    {
        ModelCapability required = ModelDefault.RequiredCapability(operation);
        return knownProviders
            .OrderBy(provider => provider.Name, StringComparer.Ordinal)
            .SelectMany(provider => provider.Models
                .Where(model => model.Capabilities.HasFlag(required))
                .OrderBy(model => model.Name, StringComparer.Ordinal)
                .Select(model => new ModelDefaultChoice(provider.Id, provider.Name, model.Name)))
            .ToArray();
    }
}
