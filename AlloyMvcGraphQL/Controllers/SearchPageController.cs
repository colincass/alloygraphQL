using AlloyMvcGraphQL.Models.Pages;
using AlloyMvcGraphQL.Models.ViewModels;
using EPiServer.ContentGraph.Api;
using EPiServer.ContentGraph.Api.Querying;
using EPiServer.ContentGraph.Extensions;
using Microsoft.AspNetCore.Mvc;
namespace AlloyMvcGraphQL.Controllers;

public class SearchPageController : PageControllerBase<SearchPage>
{
    private readonly GraphQueryBuilder _contentGraphClient;
    //private static Lazy<LocaleSerializer> _lazyLocaleSerializer = new Lazy<LocaleSerializer>(() => new LocaleSerializer());

    public SearchPageController(GraphQueryBuilder contentGraphClient)
    {
        _contentGraphClient = contentGraphClient;
    }

    public ViewResult Index(SearchPage currentPage, string q)
    {
        var searchHits = new List<SearchContentModel.SearchHit>();
        var total = 0;
        //var facets = new List<SearchContentModel.SearchFacet>();
        if (q != null)
        {
            var locale = "en"; // _lazyLocaleSerializer.Value.Parse(currentPage.Language.TwoLetterISOLanguageName.ToUpper());

            var result = _contentGraphClient
            .OperationName("SearchContentByGenericWhereClause")
            .ForType<SitePageData>()
            .Fields(_ => _.Name, _ => _.LinkURL, _ => _.MetaDescription)
            .Total()
            .GetResultAsync<SitePageData>().Result;

            var searchWords = q.Split(" ");
            //foreach (var item in result.)
            //{
            //    searchHits.Add(new SearchContentModel.SearchHit()
            //    {
            //        Title = item.Name,
            //        Url = item.LinkURL,
            //        Excerpt = item.MetaDescription.Length <= 500 ? item.MetaDescription : item.MetaDescription.Substring(0, 500)
            //    });
            //}
            //facets = result.Content.Facets["ContentType"].Select(x => new SearchContentModel.SearchFacet
            //{
            //    Name = x.Name,
            //    Count = x.Count
            //});
            //total = result.Content.Total;
        }

        var model = new SearchContentModel(currentPage)
        {
            Hits = searchHits,
            NumberOfHits = total,
            //Facets = facets,
            SearchedQuery = q
        };

        return View(model);
    }
}
