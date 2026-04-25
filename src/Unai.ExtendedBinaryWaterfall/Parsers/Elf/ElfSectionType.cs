namespace Unai.ExtendedBinaryWaterfall.Parsers.Elf;

public enum ElfSectionType
{
	Null,
	ProgramData,
	SymbolTable,
	StringTable,
	RelocationsWithAddends,
	SymbolHashTable,
	DynamicLinkingInfo,
	Notes,
	UninitializedData,
	Relocations,
	Reserved1,
	DynamicLinkerSymbolTable,
	InitArray = 0x0e,
	FiniArray = 0x0f,
	PreInitArray = 0x10,
	SectionGroup,
	ExtendedSectionIndices,
}
