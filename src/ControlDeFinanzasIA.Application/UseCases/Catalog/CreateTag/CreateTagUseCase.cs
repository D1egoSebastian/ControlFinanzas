using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Domain.Catalog;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.Logging;

namespace ControlDeFinanzasIA.Application.UseCases.Catalog.CreateTag;

/// <summary>
/// Use case that creates a new tag. It orchestrates input validation,
/// domain entity creation and persistence.
/// </summary>
/// <remarks>
/// The method returns a non-generic <see cref="Result"/>. On success, the
/// caller must cast <see cref="Result.Value"/> to <see cref="Tag"/>.
/// </remarks>
public sealed class CreateTagUseCase
{
    private readonly ITagRepository _tagRepository;
    private readonly IClock _clock;
    private readonly IValidator<CreateTagCommand> _validator;
    private readonly ILogger<CreateTagUseCase> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateTagUseCase"/> class.
    /// </summary>
    /// <param name="tagRepository">Repository used to persist the new tag.</param>
    /// <param name="clock">Clock abstraction used to source audit timestamps.</param>
    /// <param name="validator">Validator for the incoming command.</param>
    /// <param name="logger">Logger for diagnostic messages.</param>
    public CreateTagUseCase(
        ITagRepository tagRepository,
        IClock clock,
        IValidator<CreateTagCommand> validator,
        ILogger<CreateTagUseCase> logger)
    {
        _tagRepository = tagRepository;
        _clock = clock;
        _validator = validator;
        _logger = logger;
    }

    /// <summary>
    /// Executes the create-tag workflow.
    /// </summary>
    /// <param name="command">The command describing the tag to create.</param>
    /// <param name="ct">Optional cancellation token.</param>
    /// <returns>
    /// A <see cref="Result"/> that is successful when the tag is persisted,
    /// or failed with a business error (validation, domain invariant or repository error).
    /// </returns>
    public async Task<Result> ExecuteAsync(CreateTagCommand command, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        ValidationResult validation = _validator.Validate(command);
        if (!validation.IsValid)
        {
            IReadOnlyDictionary<string, string[]> details = validation.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(e => e.ErrorMessage).ToArray(),
                    StringComparer.Ordinal);

            return Result.Failure(
                "Validation",
                "El comando para crear el tag no es válido.",
                details);
        }

        Tag tag;
        try
        {
            tag = Tag.Create(command.Name.Trim(), command.Color?.Trim(), _clock.UtcNow);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Domain invariant violation while creating tag.");
            return Result.Failure("DomainInvariant", ex.Message);
        }

        Result addResult = await _tagRepository.AddAsync(tag, ct);
        if (!addResult.IsSuccess)
        {
            return addResult;
        }

        return Result.Success(tag);
    }
}
