# Photo Gallery

A single-owner photography archive, presented as a public web gallery. The owner publishes; everyone else only looks.

## Language

**Gallery**:
The site as a whole.
_Avoid_: Site, portfolio (as a noun for the app)

**Collection**:
A named, ordered group of photos. The only grouping concept in the domain; a photo belongs to exactly one Collection.
_Avoid_: Album, Series, Set, Project, Category

**Photo**:
A single image in the archive, belonging to exactly one Collection.
_Avoid_: Image, Picture, Asset, Item

**Original**:
The file the owner uploaded, kept unchanged as the archival master. Never shown to visitors.
_Avoid_: Master, Source, Raw

**Derivative**:
A generated copy of a Photo's Original at a specific width and format, served to visitors.
_Avoid_: Thumbnail, Resize, Variant, Rendition

**Upload**:
An attempt by the Admin to add a Photo to a Collection. It is not a Photo until its bytes have been accepted; an Upload may instead be rejected or expire.
_Avoid_: Job, Task, Import, File

**Featured**:
A photo the owner has chosen to appear on the home page, at an explicit rank. A curation mark, not a second Collection.
_Avoid_: Highlight, Starred, Pinned

**Admin**:
The single authenticated owner of the archive, and the only principal allowed to change content. There are no other accounts.
_Avoid_: User, Member, Account, Owner
