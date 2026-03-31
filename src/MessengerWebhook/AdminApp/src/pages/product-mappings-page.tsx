import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useMemo, useState } from "react";
import { useAuth } from "../app/auth";
import { api } from "../lib/api";
import { formatDate, formatMoney } from "../lib/format";

export function ProductMappingsPage() {
  const { csrfToken } = useAuth();
  const queryClient = useQueryClient();
  const [search, setSearch] = useState("");
  const [mappingInputs, setMappingInputs] = useState<Record<string, { productId: string; weight: string }>>({});
  const productsQuery = useQuery({
    queryKey: ["product-mappings", search],
    queryFn: () => api.getProductMappings(search)
  });

  const nobitaOptionsQuery = useQuery({
    queryKey: ["nobita-products", search],
    queryFn: () => api.searchNobitaProducts(search)
  });

  const syncMutation = useMutation({
    mutationFn: () => api.syncNobitaProducts(search, csrfToken),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ["product-mappings"] });
    }
  });

  const updateMutation = useMutation({
    mutationFn: (params: { id: string; nobitaProductId: number; nobitaWeight: number }) =>
      api.updateProductMapping(params.id, params.nobitaProductId, params.nobitaWeight, csrfToken),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ["product-mappings"] });
    }
  });

  const nobitaOptions = useMemo(() => nobitaOptionsQuery.data ?? [], [nobitaOptionsQuery.data]);

  return (
    <section className="page-section">
      <div className="page-header">
        <div>
          <span className="eyebrow">Catalog operations</span>
          <h1>Product mapping</h1>
        </div>
        <div className="button-row">
          <input className="search-input" onChange={(event) => setSearch(event.target.value)} placeholder="Tìm sản phẩm" value={search} />
          <button className="secondary-button" onClick={() => syncMutation.mutate()} type="button">Sync Nobita</button>
        </div>
      </div>

      {productsQuery.isLoading ? <div className="card">Đang tải mapping...</div> : null}
      {productsQuery.isError ? <div className="error-box">{(productsQuery.error as Error).message}</div> : null}

      <div className="detail-grid">
        <div className="card stack">
          <h2>Sản phẩm nội bộ</h2>
          {productsQuery.data?.map((product) => {
            const current = mappingInputs[product.id] ?? {
              productId: product.nobitaProductId?.toString() ?? "",
              weight: product.nobitaWeight.toString()
            };

            return (
              <article className="list-item" key={product.id}>
                <div className="stack tight">
                  <strong>{product.name}</strong>
                  <span>{product.code}</span>
                  <span>{formatMoney(product.basePrice)}</span>
                  <span>Sync lần cuối: {formatDate(product.nobitaLastSyncedAt)}</span>
                </div>
                <div className="mapping-form">
                  <input
                    onChange={(event) => setMappingInputs((previous) => ({ ...previous, [product.id]: { ...current, productId: event.target.value } }))}
                    placeholder="Nobita ID"
                    value={current.productId}
                  />
                  <input
                    onChange={(event) => setMappingInputs((previous) => ({ ...previous, [product.id]: { ...current, weight: event.target.value } }))}
                    placeholder="Weight"
                    value={current.weight}
                  />
                  <button
                    className="primary-button"
                    onClick={() =>
                      updateMutation.mutate({
                        id: product.id,
                        nobitaProductId: Number(current.productId),
                        nobitaWeight: Number(current.weight)
                      })
                    }
                    type="button"
                  >
                    Lưu
                  </button>
                </div>
              </article>
            );
          })}
          {updateMutation.data ? <div className="success-box">{updateMutation.data.message}</div> : null}
        </div>

        <div className="card stack">
          <h2>Gợi ý từ Nobita</h2>
          {nobitaOptions.map((option) => (
            <article className="list-item" key={option.productId}>
              <div>
                <strong>{option.name}</strong>
                <div>{option.code}</div>
              </div>
              <div>
                <span>{formatMoney(option.price)}</span>
                <div>{option.isOutOfStock ? "Hết hàng" : "Còn hàng"}</div>
              </div>
            </article>
          ))}
        </div>
      </div>
    </section>
  );
}
