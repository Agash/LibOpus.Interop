/*
 * Non-variadic entry points for libopus's control calls; see opus_interop.c. The bindings are
 * generated from this header like the rest of the library's, by generate/Generate.cs.
 */
#ifndef OPUS_INTEROP_H
#define OPUS_INTEROP_H

#include <opus.h>
#include <opus_multistream.h>
#include <opus_projection.h>

#ifdef __cplusplus
extern "C" {
#endif

OPUS_EXPORT int opus_interop_encoder_ctl(OpusEncoder *st, int request);
OPUS_EXPORT int opus_interop_decoder_ctl(OpusDecoder *st, int request);
OPUS_EXPORT int opus_interop_multistream_encoder_ctl(OpusMSEncoder *st, int request);
OPUS_EXPORT int opus_interop_multistream_decoder_ctl(OpusMSDecoder *st, int request);
OPUS_EXPORT int opus_interop_projection_encoder_ctl(OpusProjectionEncoder *st, int request);
OPUS_EXPORT int opus_interop_projection_decoder_ctl(OpusProjectionDecoder *st, int request);
OPUS_EXPORT int opus_interop_encoder_ctl_set(OpusEncoder *st, int request, opus_int32 value);
OPUS_EXPORT int opus_interop_decoder_ctl_set(OpusDecoder *st, int request, opus_int32 value);
OPUS_EXPORT int opus_interop_multistream_encoder_ctl_set(OpusMSEncoder *st, int request, opus_int32 value);
OPUS_EXPORT int opus_interop_multistream_decoder_ctl_set(OpusMSDecoder *st, int request, opus_int32 value);
OPUS_EXPORT int opus_interop_projection_encoder_ctl_set(OpusProjectionEncoder *st, int request, opus_int32 value);
OPUS_EXPORT int opus_interop_projection_decoder_ctl_set(OpusProjectionDecoder *st, int request, opus_int32 value);
OPUS_EXPORT int opus_interop_encoder_ctl_get(OpusEncoder *st, int request, opus_int32 *value);
OPUS_EXPORT int opus_interop_decoder_ctl_get(OpusDecoder *st, int request, opus_int32 *value);
OPUS_EXPORT int opus_interop_multistream_encoder_ctl_get(OpusMSEncoder *st, int request, opus_int32 *value);
OPUS_EXPORT int opus_interop_multistream_decoder_ctl_get(OpusMSDecoder *st, int request, opus_int32 *value);
OPUS_EXPORT int opus_interop_projection_encoder_ctl_get(OpusProjectionEncoder *st, int request, opus_int32 *value);
OPUS_EXPORT int opus_interop_projection_decoder_ctl_get(OpusProjectionDecoder *st, int request, opus_int32 *value);
OPUS_EXPORT int opus_interop_multistream_encoder_ctl_get_state(OpusMSEncoder *st, int request, opus_int32 stream, OpusEncoder **state);
OPUS_EXPORT int opus_interop_multistream_decoder_ctl_get_state(OpusMSDecoder *st, int request, opus_int32 stream, OpusDecoder **state);
OPUS_EXPORT int opus_interop_projection_encoder_ctl_get_matrix(OpusProjectionEncoder *st, int request, unsigned char *matrix, opus_int32 size);

#ifdef __cplusplus
}
#endif

#endif
