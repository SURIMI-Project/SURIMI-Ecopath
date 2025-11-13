# TestsChecklist.md — Controlled Vocabularies (POSEIDON - EwE)

> Use this as a living checklist. Mark items as you add/verify tests.  
> Legend: ✅ done · ☐ pending · 🟨 partial  
> Priority tags: `<todo version="v1">`, `<todo version="v1.1">`, `<todo version="v2">`

---

## Conventions
- **Framework**: xUnit + FluentAssertions
- **Naming**: `{Class}_{Scenario}_{ExpectedOutcome}`
- **Folders**: mirror src structure — e.g. `Descriptors/`, `ForeignKeys/`, `Match/`, `Vocabularies/`, `Resolve/`
- **Parallelism**: Debug single test while developing; suite may run in parallel in CI
- **Data**: Prefer in-memory test vocabs (`TestSourceWithField/Fields`) over external files unless loader tests

---

## v1 — Core correctness

### FieldPolicy & Normalization
- [ ] `FieldPolicy_ForSchema_Idempotent_And_Collapses_Variants` `<todo version="v1">`
- [ ] `FieldPolicy_ForValue_Code_Uppercase_Invariant_ExactCompare`
- [ ] `FieldPolicy_ForValue_Uri_Preserved_And_CaseSensitive_When_Set`
- [ ] `FieldPolicy_ForValue_Label_Lowercased_Tokenized`

### KeyFieldIndexer (Kind + Strategy + Weights)
- [ ] `Indexer_Infers_Kind_For_Code_Label_Uri_Numeric` `<todo version="v1">`
- [ ] `Indexer_Strategy_For_Code_Includes_Exact_Only`
- [ ] `Indexer_Strategy_For_Label_Includes_Fuzzy_And_TokenOverlap`
- [ ] `Indexer_Strategy_For_Uri_Is_None`
- [ ] `Indexer_Strategy_For_Numeric_Includes_NumericRange`
- [ ] `Indexer_AutoWeight_Stable_And_Bounded_1_10`

### KeyFieldDescriptor & Registry
- [ ] `Descriptor_Respects_Explicit_Kind_And_Strategy_No_Override_By_Indexer`
- [ ] `Registry_Register_Get_GetAll_ByDomain`

### MultiLevelKey
- [ ] `MLK_FromPairs_Strict_Drops_Unregistered_Fields`
- [ ] `MLK_FromPairs_Loose_Keeps_Unregistered_Fields`
- [ ] `MLK_SetField_Uses_ForSchema_On_Names`
- [ ] `MLK_ToString_Parse_Roundtrip_Preserves_Fields`
- [ ] `MLK_Freeze_Blocks_Mutation_And_IsFrozen_True`

### Foreign Keys (specs & APIs)
- [ ] `SetForeignKey_Populates_QuerySurface` `<todo version="v1">`
- [ ] `SetForeignKey_Idempotent_And_Replace_Works`
- [ ] `RemoveForeignKey_ByField_And_ByTarget_Updates_QuerySurface`
- [ ] `ForeignKeySpec_Equality_Ignores_Strict_When_Using_SameTargetIgnoringStrict`
- [ ] `ForeignKeySpec_Is_SchemaSafe_In_Ctor_And_Init`

### VocabularyRegistry
- [ ] `Registry_Register_Unregister_Get_ByName_And_Alias`
- [ ] `Registry_GetByDomain_And_GetByPurpose`
- [ ] `Registry_GetCompatibleVocabularies_DomainPurpose_Match`

### StrategyKeyResolver & Matchers
- [ ] `Resolver_Exact_Uses_ForValue_By_Kind_Code`
- [ ] `Resolver_Fuzzy_Treats_Values_As_Label_Only`
- [ ] `Resolver_BestMatch_TieBreak_ScoreThenKey`
- [ ] `Resolver_FindAll_Respects_MinScore_Threshold`

### GenericVocabularyMatcher
- [ ] `Matcher_Uses_FK_FastPath_Score100_With_Metadata` `<todo version="v1">`
- [ ] `Matcher_FallsBack_To_Strategy_When_No_FK`
- [ ] `Matcher_Overload_ByNames_Resolves_Via_Registry`
- [ ] `Matcher_Returns_NoMatch_When_Below_Threshold`
- [ ] `Matcher_Result_Always_Sets_SourceTarget_Vocab_And_Fields`

### Loaders (bounded, sanity only)
- [ ] `SURIMI_Loads_Schema_And_Records`
- [ ] `ASFIS_Loads_Schema_And_Records`
- [ ] `ISO3166_Loads_Schema_And_Records`
- [ ] `ISSCFG_Schema_Selected_Columns_Only` (when implemented)

---

## v1 — Read-only & Concurrency

### Immutable snapshot & Freeze
- [ ] `Vocabulary_Records_Are_ImmutableArray_And_ReadOnly`
- [ ] `Vocabulary_Records_Elements_Are_Frozen`
- [ ] `Descriptors_Stable_PostLoad` (if `Seal()` added, otherwise doc-only)

### Concurrency smoke (readers)
- [ ] `Concurrent_Readers_FindCode_GetFieldDescriptor_Enumerate_Records_NoExceptions`
- [ ] `Registry_Concurrent_Reads_Are_Safe`

> **Note:** These are smoke tests; not stress/perf. Use `Parallel.For` with 50–100 iterations.

---

## v1.1 — Async foundations (opt-in once core is green)

- [ ] `Vocabulary_LoadAsync_Equivalent_To_Load` (results & descriptors)
- [ ] `Vocabulary_LoadAsync_Honors_CancellationToken`
- [ ] `Resolver_LongScan_Respects_Cancellation_If_Introduced`
- [ ] `Async_Does_Not_Deadlock_In_SyncContexts` (e.g., `ConfigureAwait(false)` audited)

---

## v2 — Design hardening (optional roadmap)

- [ ] `Publish_MultiLevelKeySnapshot_Immutable_And_ValueComparable`
- [ ] `NumericRangeFieldMatcher_Basic_Intervals`
- [ ] `KindAware_CanMatch_FieldKind_Compat_Checks` (enforceKind=true path)
- [ ] `Registry_Is_ConcurrentDictionary_With_Snapshot_Enumeration`
- [ ] `Provenance_Metadata_Serialized_And_Validated`

---

## Integration (EwE bridge)

- [ ] `EwEConfiguration_Resolves_Groups_By_SpeciesCode`
- [ ] `EwEConfiguration_Resolves_Fleets_By_Gear_Flag`
- [ ] `EwEConfiguration_Resolves_Markets_By_Gear_Market`
- [ ] `EwEConfiguration_ExternalFleets_Set_And_Get`
- [ ] `EwEConfiguration_ReadSpeciesMappings_Uses_Vocab_FindCode_Fallback`

---

## CI / Infrastructure

- [ ] `.runsettings` present; VS set to single-thread when debugging
- [ ] `xunit.runner.json` (optional) to cap threads locally
- [ ] Test data minimal; large CSV/JSON only for loader tests
- [ ] Code style nudges for `is not null` in `.editorconfig`

```ini
# .editorconfig snippet
[*.cs]
dotnet_style_prefer_is_not_expression = true:suggestion
dotnet_style_prefer_is_null_check_over_reference_equality_method = true:suggestion
```

---

## Nice-to-have diagnostics

- [ ] Debug trace on FK fast-path hits (`[POSEIDON] fk-hit source.field → target.field`)
- [ ] Snapshot index stats in a small table in test output for core vocabs

---

### Pinned example tests (names you can reuse)
- `FK_FastPath_Resolves_Score100_With_Names`
- `Indexer_Infers_Uri_As_NoMatchable`
- `Exact_Compare_Uses_Code_Normalization`
- `Records_Are_Frozen`
- `Matcher_Overload_ByNames_Resolves_Via_Registry`

---

> Keep this file close to the test project and update per PR.  
> When a section is ✅, consider bumping to v1.1 items.
