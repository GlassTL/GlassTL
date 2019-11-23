GlassTL
-------------------------------

GLassTL is aimed at providing an easy way for developers to access the current layer of the Telegram API without having to do all the nitty gritty work of implementing MTProto themselves.  Due to the fact that it's written using Dotnet Core, GlassTL is multiplatform by default.  Most of the api methods are easily accessible but if you do need to send requests that are not provided, you can still build and send using the underlying connection yourself.

What is supported:
	- Telegram
		- MTProto 2.0
		- Layer 105
		- 2FA Cloud Passwords
		- Secret Chats (ToDo)
		- Multiple Transport Methods (TCP, Websocket, etc) (ToDo)
		- Upload/Download Files (ToDo)
		- And More
	- Code
		- Async Operations
		- Fully event driven
		- Dynamic Schema Objects

This project has been influenced, largely or in part, by the great work of:
	- [Lonami & Telethon](https://github.com/LonamiWebs/Telethon)
	- [sochix & TLSharp](https://github.com/sochix/TLSharp)
	- [Daniil & MadelineProto](https://github.com/danog/MadelineProto)
	- [Telegram Official Clients](https://telegram.org/apps#source-code)

Warning: GlassTL is currectly in an Alpha stage.  This means that you cannot expect anything to work properly or efficiently and that current ways of doing things are likely to change.  During this time, suggestions and feedback are welcome.  If you'd like to be a star player, you can even help by adding to the features yourself.  Documentation and Wiki, of course, needs to be added as well as code examples.  Remember that everyone who contributes also has lives of their own and that means updates may not come when you expect.

Please join the discussions on [Telegram](https://t.me/GlassTL) if you'd like to be a more personal part of GlassTL.