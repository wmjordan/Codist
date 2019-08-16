namespace Codist.Taggers
{
	interface IReuseableTagger {
		void AddRef();
		void Release();
	}
}
