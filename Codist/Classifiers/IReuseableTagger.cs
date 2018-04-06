namespace Codist.Classifiers
{
	interface IReuseableTagger {
		void AddRef();
		void Release();
	}
}
