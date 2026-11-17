using Payroll_CRM_API.Data;
using Payroll_CRM_API.Models;

namespace Payroll_CRM_API.Services
{
    public interface IProductService
    {
        Task<IEnumerable<ProductDto>> GetAllActiveProductsAsync(CancellationToken cancellationToken = default);
        Task<IEnumerable<ProductDto>> GetProductsForUserAsync(int userId, CancellationToken cancellationToken = default);
    }

    public class ProductService : IProductService
    {
        private readonly IProductRepository _productRepository;

        public ProductService(IProductRepository productRepository)
        {
            _productRepository = productRepository;
        }

        public async Task<IEnumerable<ProductDto>> GetAllActiveProductsAsync(CancellationToken cancellationToken = default)
        {
            var products = await _productRepository.GetAllActiveProductsAsync(cancellationToken);
            return products.Select(p => new ProductDto
            {
                ProductId = p.ProductId,
                ProductCode = p.ProductCode,
                ProductName = p.ProductName,
                Description = p.Description,
                IconName = p.IconName,
                ThemeColor = p.ThemeColor
            });
        }

        public async Task<IEnumerable<ProductDto>> GetProductsForUserAsync(int userId, CancellationToken cancellationToken = default)
        {
            var products = await _productRepository.GetProductsByUserIdAsync(userId, cancellationToken);
            return products.Select(p => new ProductDto
            {
                ProductId = p.ProductId,
                ProductCode = p.ProductCode,
                ProductName = p.ProductName,
                Description = p.Description,
                IconName = p.IconName,
                ThemeColor = p.ThemeColor
            });
        }
    }
}

