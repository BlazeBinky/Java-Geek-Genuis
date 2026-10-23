#include "llama.h"
#include <windows.h>
#include <algorithm>
#include <mutex>
#include <string>
#include <vector>

namespace {
std::mutex g_mutex;
llama_model* g_model = nullptr;
llama_context* g_context = nullptr;
const llama_vocab* g_vocab = nullptr;
bool g_backend_ready = false;
const uint32_t kContextTokens = 512;

std::string WideToUtf8(const wchar_t* source) {
    if (!source) return std::string();
    const int required = WideCharToMultiByte(CP_UTF8, 0, source, -1, nullptr, 0, nullptr, nullptr);
    if (required <= 1) return std::string();
    std::string result(static_cast<size_t>(required), '\0');
    const int written = WideCharToMultiByte(CP_UTF8, 0, source, -1, &result[0], required, nullptr, nullptr);
    if (written <= 1) return std::string();
    result.resize(static_cast<size_t>(written - 1));
    return result;
}

int Utf8ToWideOutput(const std::string& source, wchar_t* output, int capacity) {
    if (!output || capacity <= 0) return -1;
    output[0] = L'\0';
    if (source.empty()) return 0;
    const int required = MultiByteToWideChar(CP_UTF8, 0, source.data(), static_cast<int>(source.size()), nullptr, 0);
    if (required <= 0) return -2;
    const int count = (std::min)(required, capacity - 1);
    if (count <= 0) return 0;
    const int written = MultiByteToWideChar(CP_UTF8, 0, source.data(), static_cast<int>(source.size()), output, count);
    if (written <= 0) return -3;
    output[written] = L'\0';
    return written;
}

void FreeModelLocked() {
    g_vocab = nullptr;
    if (g_context) {
        llama_free(g_context);
        g_context = nullptr;
    }
    if (g_model) {
        llama_model_free(g_model);
        g_model = nullptr;
    }
}

bool FillBatchToken(llama_batch& batch, int index, llama_token token, llama_pos position, bool want_logits) {
    if (!batch.token || !batch.pos || !batch.n_seq_id || !batch.seq_id || !batch.logits) return false;
    batch.token[index] = token;
    batch.pos[index] = position;
    batch.n_seq_id[index] = 1;
    batch.seq_id[index][0] = 0;
    batch.logits[index] = want_logits ? 1 : 0;
    return true;
}
}

extern "C" __declspec(dllexport) int __cdecl PongoLlmLoad(const wchar_t* modelPath) {
    std::lock_guard<std::mutex> guard(g_mutex);
    FreeModelLocked();
    if (!modelPath || !modelPath[0]) return -1;

    if (!g_backend_ready) {
        llama_backend_init();
        g_backend_ready = true;
    }

    const std::string path = WideToUtf8(modelPath);
    if (path.empty()) return -2;
    llama_model_params modelParams = llama_model_default_params();
    modelParams.n_gpu_layers = 0;
    g_model = llama_model_load_from_file(path.c_str(), modelParams);
    if (!g_model) return -3;

    llama_context_params contextParams = llama_context_default_params();
    contextParams.n_ctx = kContextTokens;
    contextParams.n_batch = 64;
    contextParams.n_ubatch = 64;
    contextParams.n_threads = 2;
    contextParams.n_threads_batch = 2;
    contextParams.offload_kqv = false;
    contextParams.no_perf = true;
    g_context = llama_init_from_model(g_model, contextParams);
    if (!g_context) {
        llama_model_free(g_model);
        g_model = nullptr;
        return -4;
    }
    g_vocab = llama_model_get_vocab(g_model);
    return g_vocab ? 1 : -5;
}

extern "C" __declspec(dllexport) int __cdecl PongoLlmIsLoaded() {
    std::lock_guard<std::mutex> guard(g_mutex);
    return (g_model && g_context && g_vocab) ? 1 : 0;
}

extern "C" __declspec(dllexport) int __cdecl PongoLlmGenerate(
    const wchar_t* prompt,
    wchar_t* output,
    int outputCapacity,
    int maxTokens) {
    if (!output || outputCapacity <= 0) return -1;
    output[0] = L'\0';
    std::lock_guard<std::mutex> guard(g_mutex);
    if (!g_model || !g_context || !g_vocab) return -2;
    const std::string text = WideToUtf8(prompt);
    if (text.empty()) return -3;

    std::vector<llama_token> tokens(text.size() + 16);
    int32_t tokenCount = llama_tokenize(g_vocab, text.c_str(), static_cast<int32_t>(text.size()),
        tokens.data(), static_cast<int32_t>(tokens.size()), false, true);
    if (tokenCount < 0) {
        tokens.resize(static_cast<size_t>(-tokenCount));
        tokenCount = llama_tokenize(g_vocab, text.c_str(), static_cast<int32_t>(text.size()),
            tokens.data(), static_cast<int32_t>(tokens.size()), false, true);
    }
    if (tokenCount <= 0) return -4;
    tokens.resize(static_cast<size_t>(tokenCount));

    const int safeMax = (std::max)(1, (std::min)(maxTokens, 160));
    const int maxPromptTokens = static_cast<int>(kContextTokens) - safeMax - 2;
    if (static_cast<int>(tokens.size()) > maxPromptTokens) {
        tokens.erase(tokens.begin(), tokens.begin() + (tokens.size() - static_cast<size_t>(maxPromptTokens)));
    }
    if (tokens.empty()) return -5;

    llama_memory_clear(llama_get_memory(g_context), true);
    llama_batch promptBatch = llama_batch_init(static_cast<int32_t>(tokens.size()), 0, 1);
    if (!promptBatch.token || !promptBatch.pos || !promptBatch.n_seq_id || !promptBatch.seq_id || !promptBatch.logits) {
        llama_batch_free(promptBatch);
        return -6;
    }
    bool batchOkay = true;
    for (int i = 0; i < static_cast<int>(tokens.size()); ++i) {
        if (!FillBatchToken(promptBatch, i, tokens[static_cast<size_t>(i)], i, i == static_cast<int>(tokens.size()) - 1)) {
            batchOkay = false;
            break;
        }
    }
    int32_t decodeStatus = batchOkay ? llama_decode(g_context, promptBatch) : -1;
    llama_batch_free(promptBatch);
    if (decodeStatus != 0) return -7;

    llama_sampler_chain_params samplerParams = llama_sampler_chain_default_params();
    llama_sampler* sampler = llama_sampler_chain_init(samplerParams);
    if (!sampler) return -8;
    llama_sampler_chain_add(sampler, llama_sampler_init_top_k(40));
    llama_sampler_chain_add(sampler, llama_sampler_init_top_p(0.9f, 1));
    llama_sampler_chain_add(sampler, llama_sampler_init_temp(0.75f));
    llama_sampler_chain_add(sampler, llama_sampler_init_dist(LLAMA_DEFAULT_SEED));

    std::string generated;
    generated.reserve(static_cast<size_t>(safeMax) * 8);
    int position = static_cast<int>(tokens.size());
    int generatedCount = 0;
    for (; generatedCount < safeMax; ++generatedCount) {
        const llama_token sampled = llama_sampler_sample(sampler, g_context, -1);
        llama_sampler_accept(sampler, sampled);
        if (llama_vocab_is_eog(g_vocab, sampled)) break;

        char piece[512];
        const int32_t pieceSize = llama_token_to_piece(g_vocab, sampled, piece, static_cast<int32_t>(sizeof(piece)), 0, false);
        if (pieceSize > 0) generated.append(piece, static_cast<size_t>(pieceSize));

        llama_batch nextBatch = llama_batch_init(1, 0, 1);
        if (!nextBatch.token || !nextBatch.pos || !nextBatch.n_seq_id || !nextBatch.seq_id || !nextBatch.logits) {
            llama_batch_free(nextBatch);
            break;
        }
        const bool valid = FillBatchToken(nextBatch, 0, sampled, position, true);
        decodeStatus = valid ? llama_decode(g_context, nextBatch) : -1;
        llama_batch_free(nextBatch);
        if (decodeStatus != 0) break;
        ++position;
        if (position >= static_cast<int>(kContextTokens) - 1) break;
    }

    llama_sampler_free(sampler);
    if (generated.empty()) return -9;
    const int written = Utf8ToWideOutput(generated, output, outputCapacity);
    return written >= 0 ? generatedCount : -10;
}

extern "C" __declspec(dllexport) void __cdecl PongoLlmUnload() {
    std::lock_guard<std::mutex> guard(g_mutex);
    FreeModelLocked();
}
