namespace YourApp.Api.Contracts.Products;

// DTO của API tách khỏi command: đổi hợp đồng HTTP không kéo theo đổi tầng Application và ngược lại.
public sealed record CreateProductRequest(string Name, string Sku, decimal Price);

public sealed record UpdateProductRequest(string Name, decimal Price);
