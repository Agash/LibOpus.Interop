/*
 * Non-variadic entry points for libopus's control calls.
 *
 * opus_encoder_ctl and its siblings are C variadic functions. .NET cannot call a variadic function
 * portably: the arm64 Apple ABI passes variadic arguments on the stack where fixed ones go in
 * registers, so a P/Invoke declaring them as fixed arguments reads garbage there. Each entry point
 * below has a fixed signature, one per argument shape the controls take, and forwards to the
 * variadic call compiled for the platform.
 */
#include "opus_interop.h"

/* Requests with no argument, such as OPUS_RESET_STATE. */
OPUS_EXPORT int opus_interop_encoder_ctl(OpusEncoder *st, int request) { return opus_encoder_ctl(st, request); }
OPUS_EXPORT int opus_interop_decoder_ctl(OpusDecoder *st, int request) { return opus_decoder_ctl(st, request); }
OPUS_EXPORT int opus_interop_multistream_encoder_ctl(OpusMSEncoder *st, int request) { return opus_multistream_encoder_ctl(st, request); }
OPUS_EXPORT int opus_interop_multistream_decoder_ctl(OpusMSDecoder *st, int request) { return opus_multistream_decoder_ctl(st, request); }
OPUS_EXPORT int opus_interop_projection_encoder_ctl(OpusProjectionEncoder *st, int request) { return opus_projection_encoder_ctl(st, request); }
OPUS_EXPORT int opus_interop_projection_decoder_ctl(OpusProjectionDecoder *st, int request) { return opus_projection_decoder_ctl(st, request); }

/* OPUS_SET_*: one opus_int32 value. */
OPUS_EXPORT int opus_interop_encoder_ctl_set(OpusEncoder *st, int request, opus_int32 value) { return opus_encoder_ctl(st, request, value); }
OPUS_EXPORT int opus_interop_decoder_ctl_set(OpusDecoder *st, int request, opus_int32 value) { return opus_decoder_ctl(st, request, value); }
OPUS_EXPORT int opus_interop_multistream_encoder_ctl_set(OpusMSEncoder *st, int request, opus_int32 value) { return opus_multistream_encoder_ctl(st, request, value); }
OPUS_EXPORT int opus_interop_multistream_decoder_ctl_set(OpusMSDecoder *st, int request, opus_int32 value) { return opus_multistream_decoder_ctl(st, request, value); }
OPUS_EXPORT int opus_interop_projection_encoder_ctl_set(OpusProjectionEncoder *st, int request, opus_int32 value) { return opus_projection_encoder_ctl(st, request, value); }
OPUS_EXPORT int opus_interop_projection_decoder_ctl_set(OpusProjectionDecoder *st, int request, opus_int32 value) { return opus_projection_decoder_ctl(st, request, value); }

/* OPUS_GET_*: a pointer to an opus_int32 or opus_uint32 (OPUS_GET_FINAL_RANGE), the same size. */
OPUS_EXPORT int opus_interop_encoder_ctl_get(OpusEncoder *st, int request, opus_int32 *value) { return opus_encoder_ctl(st, request, value); }
OPUS_EXPORT int opus_interop_decoder_ctl_get(OpusDecoder *st, int request, opus_int32 *value) { return opus_decoder_ctl(st, request, value); }
OPUS_EXPORT int opus_interop_multistream_encoder_ctl_get(OpusMSEncoder *st, int request, opus_int32 *value) { return opus_multistream_encoder_ctl(st, request, value); }
OPUS_EXPORT int opus_interop_multistream_decoder_ctl_get(OpusMSDecoder *st, int request, opus_int32 *value) { return opus_multistream_decoder_ctl(st, request, value); }
OPUS_EXPORT int opus_interop_projection_encoder_ctl_get(OpusProjectionEncoder *st, int request, opus_int32 *value) { return opus_projection_encoder_ctl(st, request, value); }
OPUS_EXPORT int opus_interop_projection_decoder_ctl_get(OpusProjectionDecoder *st, int request, opus_int32 *value) { return opus_projection_decoder_ctl(st, request, value); }

/* OPUS_MULTISTREAM_GET_*_STATE: a stream index and where its state goes. */
OPUS_EXPORT int opus_interop_multistream_encoder_ctl_get_state(OpusMSEncoder *st, int request, opus_int32 stream, OpusEncoder **state) { return opus_multistream_encoder_ctl(st, request, stream, state); }
OPUS_EXPORT int opus_interop_multistream_decoder_ctl_get_state(OpusMSDecoder *st, int request, opus_int32 stream, OpusDecoder **state) { return opus_multistream_decoder_ctl(st, request, stream, state); }

/* OPUS_PROJECTION_GET_DEMIXING_MATRIX: where the matrix goes and its size. */
OPUS_EXPORT int opus_interop_projection_encoder_ctl_get_matrix(OpusProjectionEncoder *st, int request, unsigned char *matrix, opus_int32 size) { return opus_projection_encoder_ctl(st, request, matrix, size); }
