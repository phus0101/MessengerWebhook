import { useMutation, useQuery } from "@tanstack/react-query";
import { useState } from "react";
import { api } from "../lib/api";
import { useAuth } from "../app/auth";

export function VectorSearchPage() {
  const { antiForgeryToken } = useAuth();
  const [jobId, setJobId] = useState<string | null>(null);

  const startMutation = useMutation({
    mutationFn: () => api.startIndexing(antiForgeryToken),
    onSuccess: (data) => {
      setJobId(data.jobId);
    }
  });

  const statusQuery = useQuery({
    queryKey: ["indexing-status", jobId],
    queryFn: () => api.getIndexingStatus(jobId!),
    enabled: !!jobId,
    refetchInterval: (query) => {
      const status = query.state.data?.status;
      return status === "Running" ? 2000 : false;
    }
  });

  const status = statusQuery.data;
  const isRunning = status?.status === "Running";
  const isCompleted = status?.status === "Completed";
  const isFailed = status?.status === "Failed";

  return (
    <section className="page-section">
      <div className="page-header">
        <div>
          <span className="eyebrow">Vector Search</span>
          <h1>Pinecone Product Indexing</h1>
        </div>
      </div>

      <div className="card">
        <h2>Index Products to Pinecone</h2>
        <p className="text-secondary">
          Index all products to Pinecone vector database for semantic search.
        </p>

        <div className="mt-4">
          <button
            className="btn-primary"
            onClick={() => startMutation.mutate()}
            disabled={startMutation.isPending || isRunning}
          >
            {startMutation.isPending ? "Starting..." : isRunning ? "Indexing..." : "Start Indexing"}
          </button>
        </div>

        {startMutation.isError && (
          <div className="error-box mt-4">
            {(startMutation.error as Error).message}
          </div>
        )}

        {status && (
          <div className="mt-6">
            <div className="flex items-center justify-between mb-2">
              <span className="font-medium">Progress</span>
              <span className="text-sm text-secondary">
                {status.indexedProducts} / {status.totalProducts} products
              </span>
            </div>

            <div className="progress-bar">
              <div
                className="progress-fill"
                style={{ width: `${status.progressPercentage}%` }}
              />
            </div>

            <div className="text-sm text-secondary mt-2">
              {status.progressPercentage}%
            </div>

            {status.currentProductName && (
              <div className="mt-4 p-3 bg-gray-50 rounded">
                <div className="text-sm text-secondary">Currently indexing:</div>
                <div className="font-medium">{status.currentProductName}</div>
                <div className="text-xs text-secondary">{status.currentProductId}</div>
              </div>
            )}

            {isCompleted && (
              <div className="success-box mt-4">
                ✓ Indexing completed successfully!
              </div>
            )}

            {isFailed && (
              <div className="error-box mt-4">
                Indexing failed: {status.errorMessage}
              </div>
            )}
          </div>
        )}
      </div>
    </section>
  );
}
