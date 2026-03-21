namespace Unai.ExtendedBinaryWaterfall.Parsers.ScratchProject;

public enum ScratchProjectObjectClass
{
	Nil = 1,
	True,
	False,
	SmallInteger, // 32-bit
	SmallInteger16,
	LargePositiveInteger, // 16-bit length field, followed by length-many bytes
	LargeNegativeInteger,
	Float, // 64-bit

	String = 9,
	Symbol,
	ByteArray,
	SoundBuffer,
	Bitmap,
	UTF8,

	Array = 20,
	OrderedCollection,
	Set,
	IdentitySet,
	Dictionary,
	IdentityDictionary,

	Color = 30,
	TranslucentColor,
	Point,
	Rectangle,
	Form,
	ColorForm,

	ObjectReference = 99,

	Morph = 100,
	BorderedMorph,
	RectangleMorph,
	EllipseMorph,
	AlignmentMorph,
	StringMorph,
	UpdatingStringMorph,
	SimpleSliderMorph,
	SimpleButtonMorph,
	SampledSound,
	ImageMorph,
	SketchMorph,

	ScriptableScratchMorph = 122, // unconfirmed
	SensorBoardMorph = 123,
	ScratchSpriteMorph = 124,
	ScratchStageMorph = 125,

	ScratchScriptsMorph = 153,
	ScratchSliderMorph = 154,
	WatcherMorph = 155,

	ScratchMedia = 161, // unconfirmed
	ImageMedia = 162,
	MovieMedia = 163,
	SoundMedia = 164,

	MultilineStringMorph = 171,
	WatcherReadoutFrameMorph = 173,
	WatcherSliderMorph = 174,
	ScratchListMorph = 175,
}
